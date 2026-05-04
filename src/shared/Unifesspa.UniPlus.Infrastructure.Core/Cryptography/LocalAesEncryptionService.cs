namespace Unifesspa.UniPlus.Infrastructure.Core.Cryptography;

using System.Security.Cryptography;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Implementação AES-GCM 256 para dev/CI. Não usar em produção.
/// Formato do ciphertext: nonce (12 bytes) || tag (16 bytes) || dados cifrados.
/// </summary>
internal sealed partial class LocalAesEncryptionService : IUniPlusEncryptionService
{
    private const int NonceSizeBytes = 12;
    private const int TagSizeBytes = 16;

    private readonly byte[] _key;
    private readonly ILogger<LocalAesEncryptionService> _logger;

    public LocalAesEncryptionService(IOptions<EncryptionOptions> options, ILogger<LocalAesEncryptionService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;

        string localKey = options.Value.LocalKey
            ?? throw new InvalidOperationException(
                "UniPlus:Encryption:LocalKey é obrigatório quando Provider = 'local'. " +
                "Defina via env var UNIPLUS__ENCRYPTION__LOCALKEY (base64, 32 bytes).");

        byte[] keyBytes;
        try
        {
            keyBytes = Convert.FromBase64String(localKey);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException(
                "UniPlus:Encryption:LocalKey não é uma string Base64 válida.", ex);
        }

        if (keyBytes.Length != 32)
        {
            throw new InvalidOperationException(
                $"UniPlus:Encryption:LocalKey deve ter 32 bytes (256 bits). Recebido: {keyBytes.Length} bytes.");
        }

        _key = keyBytes;
    }

    public Task<byte[]> EncryptAsync(string keyName, byte[] plaintext, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(keyName);
        ArgumentNullException.ThrowIfNull(plaintext);

        try
        {
            byte[] nonce = new byte[NonceSizeBytes];
            RandomNumberGenerator.Fill(nonce);

            byte[] ciphertext = new byte[plaintext.Length];
            byte[] tag = new byte[TagSizeBytes];

            using AesGcm aes = new(_key, TagSizeBytes);
            aes.Encrypt(nonce, plaintext, ciphertext, tag);

            byte[] result = new byte[NonceSizeBytes + TagSizeBytes + ciphertext.Length];
            nonce.CopyTo(result, 0);
            tag.CopyTo(result, NonceSizeBytes);
            ciphertext.CopyTo(result, NonceSizeBytes + TagSizeBytes);

            LogEncrypt(_logger, keyName);
            return Task.FromResult(result);
        }
        catch (Exception ex) when (ex is not EncryptionFailureException)
        {
            throw new EncryptionFailureException(keyName, ex);
        }
    }

    public Task<byte[]> DecryptAsync(string keyName, byte[] ciphertext, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(keyName);
        ArgumentNullException.ThrowIfNull(ciphertext);

        int minLength = NonceSizeBytes + TagSizeBytes + 1;
        if (ciphertext.Length < minLength)
        {
            throw new EncryptionFailureException(keyName,
                new CryptographicException("Ciphertext inválido: tamanho insuficiente."));
        }

        try
        {
            ReadOnlySpan<byte> nonce = ciphertext.AsSpan(0, NonceSizeBytes);
            ReadOnlySpan<byte> tag = ciphertext.AsSpan(NonceSizeBytes, TagSizeBytes);
            ReadOnlySpan<byte> encrypted = ciphertext.AsSpan(NonceSizeBytes + TagSizeBytes);

            byte[] plaintext = new byte[encrypted.Length];

            using AesGcm aes = new(_key, TagSizeBytes);
            aes.Decrypt(nonce, encrypted, tag, plaintext);

            LogDecrypt(_logger, keyName);
            return Task.FromResult(plaintext);
        }
        catch (CryptographicException ex)
        {
            throw new EncryptionFailureException(keyName, ex);
        }
        catch (Exception ex) when (ex is not EncryptionFailureException)
        {
            throw new EncryptionFailureException(keyName, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Encrypt concluído para chave '{KeyName}'")]
    private static partial void LogEncrypt(ILogger logger, string keyName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Decrypt concluído para chave '{KeyName}'")]
    private static partial void LogDecrypt(ILogger logger, string keyName);
}
