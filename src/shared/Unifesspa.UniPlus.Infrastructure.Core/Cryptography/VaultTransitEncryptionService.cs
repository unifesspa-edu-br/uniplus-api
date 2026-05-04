namespace Unifesspa.UniPlus.Infrastructure.Core.Cryptography;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using VaultSharp;
using VaultSharp.V1.AuthMethods.Kubernetes;
using VaultSharp.V1.Commons;
using VaultSharp.V1.SecretsEngines.Transit;

/// <summary>
/// Implementação de produção via HashiCorp Vault transit engine.
/// Chaves nunca saem do Vault; autenticação via Kubernetes auth method.
/// </summary>
internal sealed partial class VaultTransitEncryptionService : IUniPlusEncryptionService
{
    private readonly VaultClient _vault;
    private readonly string _transitMount;
    private readonly ILogger<VaultTransitEncryptionService> _logger;

    public VaultTransitEncryptionService(IOptions<EncryptionOptions> options, ILogger<VaultTransitEncryptionService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;

        EncryptionOptions opts = options.Value;

        if (string.IsNullOrWhiteSpace(opts.VaultAddress))
        {
            throw new InvalidOperationException(
                "UniPlus:Encryption:VaultAddress é obrigatório quando Provider = 'vault'.");
        }

        string jwt = File.ReadAllText(opts.KubernetesJwtPath);

        KubernetesAuthMethodInfo authMethod = new(opts.KubernetesRole ?? "uniplus-api", jwt);
        VaultClientSettings settings = new(opts.VaultAddress, authMethod);

        _vault = new VaultClient(settings);
        _transitMount = opts.VaultTransitMount;
    }

    public async Task<byte[]> EncryptAsync(string keyName, byte[] plaintext, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(keyName);
        ArgumentNullException.ThrowIfNull(plaintext);

        try
        {
            string base64Plain = Convert.ToBase64String(plaintext);
            EncryptRequestOptions request = new() { Base64EncodedPlainText = base64Plain };

            Secret<EncryptionResponse> response = await _vault.V1.Secrets.Transit
                .EncryptAsync(keyName, request, _transitMount)
                .ConfigureAwait(false);

            string ciphertext = response.Data.CipherText;
            LogEncrypt(_logger, keyName);
            return System.Text.Encoding.UTF8.GetBytes(ciphertext);
        }
        catch (Exception ex) when (ex is not EncryptionFailureException)
        {
            throw new EncryptionFailureException(keyName, ex);
        }
    }

    public async Task<byte[]> DecryptAsync(string keyName, byte[] ciphertext, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(keyName);
        ArgumentNullException.ThrowIfNull(ciphertext);

        try
        {
            string vaultCiphertext = System.Text.Encoding.UTF8.GetString(ciphertext);
            DecryptRequestOptions request = new() { CipherText = vaultCiphertext };

            Secret<DecryptionResponse> response = await _vault.V1.Secrets.Transit
                .DecryptAsync(keyName, request, _transitMount)
                .ConfigureAwait(false);

            byte[] plaintext = Convert.FromBase64String(response.Data.Base64EncodedPlainText);
            LogDecrypt(_logger, keyName);
            return plaintext;
        }
        catch (Exception ex) when (ex is not EncryptionFailureException)
        {
            throw new EncryptionFailureException(keyName, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Vault encrypt concluído para chave '{KeyName}'")]
    private static partial void LogEncrypt(ILogger logger, string keyName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Vault decrypt concluído para chave '{KeyName}'")]
    private static partial void LogDecrypt(ILogger logger, string keyName);
}
