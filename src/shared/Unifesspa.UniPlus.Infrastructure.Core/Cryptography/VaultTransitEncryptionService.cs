namespace Unifesspa.UniPlus.Infrastructure.Core.Cryptography;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using VaultSharp;
using VaultSharp.Core;
using VaultSharp.V1.AuthMethods.Kubernetes;
using VaultSharp.V1.Commons;
using VaultSharp.V1.SecretsEngines.Transit;

/// <summary>
/// Implementação de produção via HashiCorp Vault transit engine.
/// Chaves nunca saem do Vault; autenticação via Kubernetes auth method.
/// </summary>
internal sealed partial class VaultTransitEncryptionService : IUniPlusEncryptionService, IDisposable
{
    private volatile VaultClient _vault;
    private readonly string _vaultAddress;
    private readonly string _jwtPath;
    private readonly string _role;
    private readonly string _transitMount;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
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

        _vaultAddress = opts.VaultAddress;
        _jwtPath = opts.KubernetesJwtPath;
        _role = opts.KubernetesRole ?? "uniplus-api";
        _transitMount = opts.VaultTransitMount;

        _vault = CreateVaultClient();
    }

    public async Task<byte[]> EncryptAsync(string keyName, byte[] plaintext, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(keyName);
        ArgumentNullException.ThrowIfNull(plaintext);

        // VaultSharp 1.17.5.1 Transit não expõe CancellationToken — parâmetro recebido mas não propagável.
        try
        {
            return await ExecuteWithAuthRetryAsync(keyName, async vault =>
            {
                string base64Plain = Convert.ToBase64String(plaintext);
                EncryptRequestOptions request = new() { Base64EncodedPlainText = base64Plain };

                Secret<EncryptionResponse> response = await vault.V1.Secrets.Transit
                    .EncryptAsync(keyName, request, _transitMount)
                    .ConfigureAwait(false);

                LogEncrypt(_logger, keyName);
                return System.Text.Encoding.UTF8.GetBytes(response.Data.CipherText);
            }).ConfigureAwait(false);
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

        // VaultSharp 1.17.5.1 Transit não expõe CancellationToken — parâmetro recebido mas não propagável.
        try
        {
            return await ExecuteWithAuthRetryAsync(keyName, async vault =>
            {
                string vaultCiphertext = System.Text.Encoding.UTF8.GetString(ciphertext);
                DecryptRequestOptions request = new() { CipherText = vaultCiphertext };

                Secret<DecryptionResponse> response = await vault.V1.Secrets.Transit
                    .DecryptAsync(keyName, request, _transitMount)
                    .ConfigureAwait(false);

                LogDecrypt(_logger, keyName);
                return Convert.FromBase64String(response.Data.Base64EncodedPlainText);
            }).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not EncryptionFailureException)
        {
            throw new EncryptionFailureException(keyName, ex);
        }
    }

    public void Dispose() => _refreshLock.Dispose();

    private VaultClient CreateVaultClient()
    {
        string jwt = File.ReadAllText(_jwtPath);
        KubernetesAuthMethodInfo authMethod = new(_role, jwt);
        return new VaultClient(new VaultClientSettings(_vaultAddress, authMethod));
    }

    private async Task RefreshVaultClientAsync()
    {
        await _refreshLock.WaitAsync().ConfigureAwait(false);
        try
        {
            _vault = CreateVaultClient();
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    /// <summary>
    /// Executa <paramref name="operation"/> com retry automático quando o Vault retorna 403.
    /// O retry relê o JWT do disco, garantindo que tokens K8s rotacionados sejam absorvidos sem restart.
    /// </summary>
    private async Task<T> ExecuteWithAuthRetryAsync<T>(string keyName, Func<VaultClient, Task<T>> operation)
    {
        try
        {
            return await operation(_vault).ConfigureAwait(false);
        }
        catch (VaultApiException vex) when (vex.StatusCode == 403)
        {
            LogAuthRefresh(_logger, keyName);
            await RefreshVaultClientAsync().ConfigureAwait(false);
            return await operation(_vault).ConfigureAwait(false);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Vault encrypt concluído para chave '{KeyName}'")]
    private static partial void LogEncrypt(ILogger logger, string keyName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Vault decrypt concluído para chave '{KeyName}'")]
    private static partial void LogDecrypt(ILogger logger, string keyName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Vault retornou 403 para chave '{KeyName}'; recriando cliente com JWT atualizado e repetindo")]
    private static partial void LogAuthRefresh(ILogger logger, string keyName);
}
