namespace Unifesspa.UniPlus.Infrastructure.Core.Cryptography;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using VaultSharp.V1.Commons;
using VaultSharp.V1.SecretsEngines.Transit;

/// <summary>
/// Implementação de produção do índice cego via HMAC do Vault transit engine
/// (<c>transit/hmac/&lt;keyName&gt;/sha2-256</c>). A chave nunca sai do Vault.
/// </summary>
internal sealed partial class VaultTransitBlindIndexService : VaultConnectedCryptographyServiceBase, IUniPlusBlindIndexService
{
    private readonly string _transitMount;
    private readonly int _keyVersion;
    private readonly ILogger<VaultTransitBlindIndexService> _logger;

    public VaultTransitBlindIndexService(IOptions<EncryptionOptions> options, ILogger<VaultTransitBlindIndexService> logger)
        : base(RequireOptions(options))
    {
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
        _transitMount = options.Value.VaultTransitMount;
        _keyVersion = options.Value.BlindIndexKeyVersion;
    }

    public async Task<byte[]> ComputarAsync(string keyName, ReadOnlyMemory<byte> valor, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(keyName);

        // VaultSharp 1.17.5.1 Transit não expõe CancellationToken — parâmetro recebido mas não propagável.
        try
        {
            return await ExecuteWithAuthRetryAsync(async vault =>
            {
                HmacRequestOptions request = new()
                {
                    Base64EncodedInput = Convert.ToBase64String(valor.Span),
                    // Versão fixa (ADR-0121): nunca acompanha a versão mais recente da chave —
                    // trocar exige o procedimento de rotação (recálculo de todos os índices),
                    // nunca um efeito colateral de rotacionar a chave no Vault.
                    KeyVersion = _keyVersion,
                };

                Secret<HmacResponse> response = await vault.V1.Secrets.Transit
                    .GenerateHmacAsync(keyName, request, _transitMount)
                    .ConfigureAwait(false);

                LogComputado(_logger, keyName);
                return DecodificarHmac(response.Data.Hmac);
            }, () => LogAuthRefresh(_logger, keyName)).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not EncryptionFailureException)
        {
            throw new EncryptionFailureException(keyName, ex);
        }
    }

    // O Vault devolve o HMAC prefixado com a versão da chave (ex.: "vault:v1:<base64>") — a
    // issue pede o índice gravado sem esse prefixo, só o valor em si.
    private static byte[] DecodificarHmac(string hmac)
    {
        int ultimoDoisPontos = hmac.LastIndexOf(':');
        string base64 = ultimoDoisPontos >= 0 ? hmac[(ultimoDoisPontos + 1)..] : hmac;
        return Convert.FromBase64String(base64);
    }

    private static EncryptionOptions RequireOptions(IOptions<EncryptionOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.Value;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Índice cego Vault computado para chave '{KeyName}'")]
    private static partial void LogComputado(ILogger logger, string keyName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Vault retornou 403 para chave '{KeyName}'; recriando cliente com JWT atualizado e repetindo")]
    private static partial void LogAuthRefresh(ILogger logger, string keyName);
}
