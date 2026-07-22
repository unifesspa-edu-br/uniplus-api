namespace Unifesspa.UniPlus.Infrastructure.Core.Messaging.SchemaRegistry;

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

using Confluent.SchemaRegistry;

using Microsoft.Extensions.Logging;

/// <summary>
/// <see cref="IAuthenticationHeaderValueProvider"/> que obtém um access_token via OAuth
/// <c>client_credentials</c> contra o Keycloak realm <c>uniplus</c> e injeta como
/// <c>Authorization: Bearer &lt;jwt&gt;</c> em todos os requests do Schema Registry client.
/// </summary>
/// <remarks>
/// <para>
/// Cacheia o JWT em memória até <c>exp - RefreshSkewSeconds</c>; renova proativamente
/// para evitar 401 em chamadas concorrentes que peguem token quase expirado. O parsing do
/// <c>exp</c> é feito sobre o <c>expires_in</c> da response do token endpoint
/// (em segundos), não sobre o claim do JWT — independe de a presença do <c>exp</c> no
/// payload (Keycloak sempre retorna <c>expires_in</c>).
/// </para>
/// <para>
/// Renovações concorrentes são serializadas via <see cref="SemaphoreSlim"/> — apenas um
/// thread chama o token endpoint por vez, os demais reutilizam o token recém-obtido.
/// </para>
/// <para>
/// Falha ao obter token escala como exceção via <see cref="HttpRequestException"/>; o
/// <c>CachedSchemaRegistryClient</c> trata como erro de chamada (não como auth failure
/// silencioso). Logging inclui o <c>ClientId</c> mas nunca o secret.
/// </para>
/// </remarks>
public sealed partial class OAuthBearerAuthenticationHeaderValueProvider
    : IAuthenticationHeaderValueProvider, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly OAuthBearerSettings _settings;
    private readonly ILogger<OAuthBearerAuthenticationHeaderValueProvider> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _refreshLock = new(initialCount: 1, maxCount: 1);

    private string? _cachedToken;
    private DateTimeOffset _cachedTokenExpiresAt;
    private bool _disposed;

    /// <summary>
    /// Construtor padrão para uso via DI (<c>IHttpClientFactory</c> + <c>AddHttpClient</c>):
    /// o <see cref="HttpClient"/> é gerenciado pelo factory, não disposto aqui.
    /// </summary>
    public OAuthBearerAuthenticationHeaderValueProvider(
        HttpClient httpClient,
        OAuthBearerSettings settings,
        ILogger<OAuthBearerAuthenticationHeaderValueProvider> logger,
        TimeProvider timeProvider)
        : this(httpClient, ownsHttpClient: false, settings, logger, timeProvider)
    {
    }

    /// <summary>
    /// Construtor usado pelo bootstrap standalone (<c>SchemaRegistryServiceCollectionExtensions.CreateClient</c>)
    /// quando o <see cref="HttpClient"/> é criado fora do <c>IHttpClientFactory</c> —
    /// nesse caso o provider assume <i>ownership</i> e disposa o cliente em <see cref="Dispose"/>.
    /// </summary>
    internal OAuthBearerAuthenticationHeaderValueProvider(
        HttpClient httpClient,
        bool ownsHttpClient,
        OAuthBearerSettings settings,
        ILogger<OAuthBearerAuthenticationHeaderValueProvider> logger,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _httpClient = httpClient;
        _ownsHttpClient = ownsHttpClient;
        _settings = settings;
        _logger = logger;
        _timeProvider = timeProvider;
        _httpClient.Timeout = TimeSpan.FromMilliseconds(settings.RequestTimeoutMs);
    }

    public AuthenticationHeaderValue GetAuthenticationHeader()
    {
        // Confluent.SchemaRegistry chama esta API sincronamente; bloqueamos no token cache
        // ou na renovação. O cliente HTTP roda em background no Schema Registry, então o
        // bloqueio aqui é raro (só na primeira chamada e a cada janela de refresh).
        string token = GetOrRefreshTokenAsync(CancellationToken.None).GetAwaiter().GetResult();
        return new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task<string> GetOrRefreshTokenAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        TimeSpan skew = TimeSpan.FromSeconds(_settings.RefreshSkewSeconds);

        if (_cachedToken is not null && now < _cachedTokenExpiresAt - skew)
        {
            return _cachedToken;
        }

        await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Re-check após acquire — outro thread pode ter renovado enquanto esperávamos.
            now = _timeProvider.GetUtcNow();
            if (_cachedToken is not null && now < _cachedTokenExpiresAt - skew)
            {
                return _cachedToken;
            }

            TokenEndpointResponse response = await RequestTokenAsync(cancellationToken).ConfigureAwait(false);

            _cachedToken = response.AccessToken;
            _cachedTokenExpiresAt = _timeProvider.GetUtcNow().AddSeconds(response.ExpiresInSeconds);

            LogTokenRefreshed(_logger, _settings.ClientId, response.ExpiresInSeconds);

            return _cachedToken;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task<TokenEndpointResponse> RequestTokenAsync(CancellationToken cancellationToken)
    {
        List<KeyValuePair<string, string>> form =
        [
            new("grant_type", "client_credentials"),
            new("client_id", _settings.ClientId),
            new("client_secret", _settings.ClientSecret),
        ];

        if (!string.IsNullOrWhiteSpace(_settings.Scope))
        {
            form.Add(new("scope", _settings.Scope));
        }

        using FormUrlEncodedContent content = new(form);
        using HttpResponseMessage response = await _httpClient
            .PostAsync(new Uri(_settings.TokenEndpoint), content, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            int status = (int)response.StatusCode;
            LogTokenRequestFailed(_logger, _settings.ClientId, status, body);

            // 4xx = falha determinística (client_id/client_secret incorretos, scope inválido,
            // realm errado, client desabilitado). Propagar como InvalidOperationException
            // para que o consumidor (e.g. SchemaRegistrationHostedService) NÃO trate
            // como transiente — release com config ruim deve travar StartAsync.
            //
            // 5xx = falha possivelmente transiente (Keycloak sob carga, restart em curso) —
            // mantém HttpRequestException para fallback fail-graceful no caminho startup.
            if (status >= 400 && status < 500)
            {
                throw new InvalidOperationException(
                    $"OAuth client_credentials para Schema Registry recebeu status determinístico {status} "
                    + "(config inválida — verifique ClientId/ClientSecret/TokenEndpoint/Scope no realm). "
                    + $"ClientId={_settings.ClientId}.");
            }

            // 5xx do token endpoint é transitivo — Keycloak sob carga, restart em curso.
            // Marca explicitamente HttpRequestError.ConnectionError para que o
            // SchemaRegistrationHostedService trate via fail-graceful (ConnectionError/
            // NameResolutionError são os casos transientes filtrados). Sem o enum value
            // explícito, HttpRequestError fica Unknown e o catch propaga, travando
            // startup em incidente transitivo do Keycloak (boot avoidable failure).
            throw new HttpRequestException(
                HttpRequestError.ConnectionError,
                $"OAuth client_credentials para Schema Registry falhou com status transiente {status}. "
                + $"ClientId={_settings.ClientId}.");
        }

        TokenEndpointResponse? parsed = await response.Content
            .ReadFromJsonAsync<TokenEndpointResponse>(cancellationToken)
            .ConfigureAwait(false);

        if (parsed is null
            || string.IsNullOrWhiteSpace(parsed.AccessToken)
            || parsed.ExpiresInSeconds <= 0)
        {
            throw new InvalidOperationException(
                $"Resposta do token endpoint inválida. ClientId={_settings.ClientId}.");
        }

        return parsed;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _refreshLock.Dispose();
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }

        _disposed = true;
    }

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "SchemaRegistry OAuth token renovado. ClientId={ClientId} ExpiresInSeconds={ExpiresInSeconds}")]
    private static partial void LogTokenRefreshed(ILogger logger, string clientId, int expiresInSeconds);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "SchemaRegistry OAuth token endpoint falhou. ClientId={ClientId} Status={Status} Body={Body}")]
    private static partial void LogTokenRequestFailed(ILogger logger, string clientId, int status, string body);

    private sealed record TokenEndpointResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; init; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresInSeconds { get; init; }

        [JsonPropertyName("token_type")]
        public string TokenType { get; init; } = string.Empty;
    }
}
