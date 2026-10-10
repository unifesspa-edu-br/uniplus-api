namespace Unifesspa.UniPlus.Selecao.Infrastructure.Geo;

using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Guarda o token de acesso M2M ao Geo (client_credentials, Keycloak) e o renova antes de vencer.
/// </summary>
/// <remarks>
/// Mesmo desenho de <c>OAuthBearerAuthenticationHeaderValueProvider</c> (Schema Registry): a
/// validade vem de <c>expires_in</c> da própria resposta do token endpoint — Keycloak sempre o
/// devolve —, não de decodificar o JWT (diferente do <c>SigaaTokenProvider</c>, cuja origem não
/// declara validade e por isso lê o <c>exp</c> do token). Renovações concorrentes são serializadas
/// por <see cref="SemaphoreSlim"/>: a primeira requisição que encontra o token vencido renova, as
/// demais aguardam e reaproveitam.
/// </remarks>
internal sealed partial class GeoTokenProvider : IDisposable
{
    /// <summary>Nome do cliente HTTP que fala com o endpoint de token — à parte do cliente de consulta ao Geo.</summary>
    internal const string NomeDoClienteHttp = "geo-autenticacao";

    private readonly IHttpClientFactory _clientes;
    private readonly GeoOptions _opcoes;
    private readonly ILogger<GeoTokenProvider> _logger;
    private readonly TimeProvider _relogio;
    private readonly SemaphoreSlim _trava = new(initialCount: 1, maxCount: 1);

    private string? _token;
    private DateTimeOffset _expiraEm;
    private bool _descartado;

    public GeoTokenProvider(
        IHttpClientFactory clientes, IOptions<GeoOptions> opcoes, ILogger<GeoTokenProvider> logger, TimeProvider relogio)
    {
        ArgumentNullException.ThrowIfNull(clientes);
        ArgumentNullException.ThrowIfNull(opcoes);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(relogio);

        _clientes = clientes;
        _opcoes = opcoes.Value;
        _logger = logger;
        _relogio = relogio;
    }

    public async Task<string> ObterAsync(CancellationToken cancellationToken)
    {
        if (TentarReaproveitar(out string? vigente))
        {
            return vigente;
        }

        await _trava.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (TentarReaproveitar(out string? renovadoPorOutra))
            {
                return renovadoPorOutra;
            }

            TokenEndpointResponse resposta = await AutenticarAsync(cancellationToken).ConfigureAwait(false);

            _token = resposta.AccessToken;
            _expiraEm = _relogio.GetUtcNow().AddSeconds(resposta.ExpiresInSeconds);

            LogTokenRenovado(_logger, _opcoes.OAuth.ClientId, resposta.ExpiresInSeconds);

            return _token;
        }
        finally
        {
            _trava.Release();
        }
    }

    /// <summary>Descarta o token que o Geo recusou, para que a próxima requisição renove. Só descarta se ainda for o guardado.</summary>
    public void Descartar(string tokenRecusado)
    {
        _trava.Wait();
        try
        {
            if (string.Equals(_token, tokenRecusado, StringComparison.Ordinal))
            {
                _token = null;
                _expiraEm = default;
            }
        }
        finally
        {
            _trava.Release();
        }
    }

    private bool TentarReaproveitar([NotNullWhen(true)] out string? token)
    {
        string? guardado = _token;
        TimeSpan margem = TimeSpan.FromSeconds(_opcoes.OAuth.MargemDeRenovacaoEmSegundos);

        if (guardado is not null && _relogio.GetUtcNow() < _expiraEm - margem)
        {
            token = guardado;
            return true;
        }

        token = null;
        return false;
    }

    private async Task<TokenEndpointResponse> AutenticarAsync(CancellationToken cancellationToken)
    {
        using HttpClient cliente = _clientes.CreateClient(NomeDoClienteHttp);

        List<KeyValuePair<string, string>> formulario =
        [
            new("grant_type", "client_credentials"),
            new("client_id", _opcoes.OAuth.ClientId),
            new("client_secret", _opcoes.OAuth.ClientSecret),
        ];
        if (!string.IsNullOrWhiteSpace(_opcoes.OAuth.Scope))
        {
            formulario.Add(new("scope", _opcoes.OAuth.Scope));
        }

        using FormUrlEncodedContent corpo = new(formulario);
        using HttpResponseMessage resposta = await cliente
            .PostAsync(new Uri(_opcoes.OAuth.TokenEndpoint, UriKind.Absolute), corpo, cancellationToken)
            .ConfigureAwait(false);

        if (!resposta.IsSuccessStatusCode)
        {
            int status = (int)resposta.StatusCode;
            LogAutenticacaoRecusada(_logger, _opcoes.OAuth.ClientId, status);

            throw new GeoAutenticacaoException(
                $"O Keycloak recusou a autenticação client_credentials do cliente do Geo com status {status}.");
        }

        TokenEndpointResponse? conteudo = await resposta.Content
            .ReadFromJsonAsync<TokenEndpointResponse>(cancellationToken)
            .ConfigureAwait(false);

        if (conteudo is null || string.IsNullOrWhiteSpace(conteudo.AccessToken) || conteudo.ExpiresInSeconds <= 0)
        {
            throw new GeoAutenticacaoException("O Keycloak respondeu à autenticação do cliente do Geo sem um access_token válido.");
        }

        return conteudo;
    }

    public void Dispose()
    {
        if (_descartado)
        {
            return;
        }

        _trava.Dispose();
        _descartado = true;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Token de acesso ao Geo renovado. ClientId={ClientId} ExpiresInSeconds={ExpiresInSeconds}")]
    private static partial void LogTokenRenovado(ILogger logger, string clientId, int expiresInSeconds);

    [LoggerMessage(Level = LogLevel.Error, Message = "O Keycloak recusou a autenticação client_credentials do cliente do Geo. ClientId={ClientId} Status={Status}")]
    private static partial void LogAutenticacaoRecusada(ILogger logger, string clientId, int status);

    private sealed record TokenEndpointResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; init; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresInSeconds { get; init; }
    }
}
