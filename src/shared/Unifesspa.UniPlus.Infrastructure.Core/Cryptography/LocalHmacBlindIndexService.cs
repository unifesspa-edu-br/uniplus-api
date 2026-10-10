namespace Unifesspa.UniPlus.Infrastructure.Core.Cryptography;

using System.Security.Cryptography;
using System.Text;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Implementação HMAC-SHA256 para dev/CI. Não usar em produção.
/// A subchave usada no HMAC nunca é a <see cref="EncryptionOptions.LocalKey"/> diretamente —
/// é derivada por HKDF-SHA256, com <c>info</c> amarrado ao <c>keyName</c>, para que uma chave
/// nomeada só sirva ao índice cego daquele nome (mesmo <c>LocalKey</c>, chaves nomeadas
/// diferentes dão subchaves — e portanto índices — diferentes).
/// </summary>
internal sealed partial class LocalHmacBlindIndexService : IUniPlusBlindIndexService
{
    private const int SubkeySizeBytes = 32;
    private const string InfoPrefix = "uniplus-indice-cego:";

    private readonly byte[] _localKey;
    private readonly ILogger<LocalHmacBlindIndexService> _logger;

    public LocalHmacBlindIndexService(IOptions<EncryptionOptions> options, ILogger<LocalHmacBlindIndexService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;

        // Mesma LocalKey da cifra (ADR-0121) — o índice cego reaproveita o segredo já
        // provisionado, só com derivação (HKDF) própria por keyName; não é uma chave nova
        // a provisionar.
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

        _localKey = keyBytes;
    }

    public Task<byte[]> ComputarAsync(string keyName, ReadOnlyMemory<byte> valor, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(keyName);

        try
        {
            byte[] info = Encoding.UTF8.GetBytes(InfoPrefix + keyName);
            byte[] subkey = HKDF.DeriveKey(HashAlgorithmName.SHA256, _localKey, SubkeySizeBytes, info: info);

            using HMACSHA256 hmac = new(subkey);
            byte[] indice = hmac.ComputeHash(valor.ToArray());

            LogComputado(_logger, keyName);
            return Task.FromResult(indice);
        }
        catch (Exception ex) when (ex is not EncryptionFailureException)
        {
            throw new EncryptionFailureException(keyName, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Índice cego local computado para chave '{KeyName}'")]
    private static partial void LogComputado(ILogger logger, string keyName);
}
