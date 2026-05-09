namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Messaging.SchemaRegistry;

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Unifesspa.UniPlus.Infrastructure.Core.Messaging.SchemaRegistry;

/// <summary>
/// Cobertura de <see cref="OAuthBearerAuthenticationHeaderValueProvider"/>:
/// cache de JWT, refresh proativo, distinção 4xx (deterministic — propaga
/// InvalidOperationException) vs 5xx (transient — HttpRequestException com
/// HttpRequestError.ConnectionError), serialização de renovações concorrentes via
/// SemaphoreSlim, ownership de HttpClient.
/// </summary>
[Trait("Category", "Unit")]
public sealed class OAuthBearerAuthenticationHeaderValueProviderTests
{
    private static OAuthBearerSettings DefaultSettings(int refreshSkewSeconds = 30)
        => new()
        {
            TokenEndpoint = "https://kc.test/realms/uniplus/protocol/openid-connect/token",
            ClientId = "uniplus-api-selecao",
            ClientSecret = "s3cr3t",
            RefreshSkewSeconds = refreshSkewSeconds,
            RequestTimeoutMs = 1_000,
        };

    private static string TokenResponseJson(string accessToken, int expiresInSeconds)
        => JsonSerializer.Serialize(new
        {
            access_token = accessToken,
            expires_in = expiresInSeconds,
            token_type = "Bearer",
        });

    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "Factory: HttpClient é owned pelo provider (ownsHttpClient=true); ownership do provider passa ao caller via 'using'.")]
    private static OAuthBearerAuthenticationHeaderValueProvider CreateProvider(
        StubHttpMessageHandler handler,
        OAuthBearerSettings? settings = null)
    {
        HttpClient httpClient = new(handler);
        return new OAuthBearerAuthenticationHeaderValueProvider(
            httpClient,
            ownsHttpClient: true,
            settings ?? DefaultSettings(),
            NullLogger<OAuthBearerAuthenticationHeaderValueProvider>.Instance);
    }

    [Fact(DisplayName = "200 OK retorna Bearer header com access_token + cacheia o token")]
    public void HappyPath_DeveRetornarBearerEcachear()
    {
        using StubHttpMessageHandler handler = new();
        handler.EnqueueResponse(HttpStatusCode.OK, TokenResponseJson("jwt-abc", 300));
        using OAuthBearerAuthenticationHeaderValueProvider sut = CreateProvider(handler);

        AuthenticationHeaderValue header = sut.GetAuthenticationHeader();

        header.Scheme.Should().Be("Bearer");
        header.Parameter.Should().Be("jwt-abc");
        handler.CallCount.Should().Be(1);

        // Forma do request ao token endpoint
        HttpRequestMessage req = handler.ReceivedRequests[0];
        req.Method.Should().Be(HttpMethod.Post);
        req.RequestUri.Should().Be(new Uri("https://kc.test/realms/uniplus/protocol/openid-connect/token"));
    }

    [Fact(DisplayName = "Cache hit dentro da janela < exp - skew NÃO chama o token endpoint novamente")]
    public void CacheHit_DeveReusarTokenSemNovaChamada()
    {
        using StubHttpMessageHandler handler = new();
        handler.EnqueueResponse(HttpStatusCode.OK, TokenResponseJson("jwt-cached", 300));
        using OAuthBearerAuthenticationHeaderValueProvider sut = CreateProvider(handler);

        AuthenticationHeaderValue first = sut.GetAuthenticationHeader();
        AuthenticationHeaderValue second = sut.GetAuthenticationHeader();
        AuthenticationHeaderValue third = sut.GetAuthenticationHeader();

        first.Parameter.Should().Be("jwt-cached");
        second.Parameter.Should().Be("jwt-cached");
        third.Parameter.Should().Be("jwt-cached");
        handler.CallCount.Should().Be(1, because: "três chamadas dentro da janela usam o mesmo token.");
    }

    [Fact(DisplayName = "Refresh proativo após exp - skew chama o token endpoint novamente")]
    public void RefreshProativo_DeveBuscarNovoToken()
    {
        using StubHttpMessageHandler handler = new();
        // expires_in muito baixo (1s) + skew alto (30s) garante que cache invalida na 2ª chamada.
        handler.EnqueueResponse(HttpStatusCode.OK, TokenResponseJson("jwt-old", expiresInSeconds: 1));
        handler.EnqueueResponse(HttpStatusCode.OK, TokenResponseJson("jwt-new", expiresInSeconds: 300));
        using OAuthBearerAuthenticationHeaderValueProvider sut = CreateProvider(handler);

        AuthenticationHeaderValue first = sut.GetAuthenticationHeader();
        AuthenticationHeaderValue second = sut.GetAuthenticationHeader();

        first.Parameter.Should().Be("jwt-old");
        second.Parameter.Should().Be("jwt-new");
        handler.CallCount.Should().Be(2);
    }

    [Theory(DisplayName = "4xx no token endpoint propaga InvalidOperationException (deterministic)")]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    public void Status4xx_DevePropagarInvalidOperation(HttpStatusCode status)
    {
        using StubHttpMessageHandler handler = new();
        handler.EnqueueResponse(status, """{"error":"invalid_client"}""");
        using OAuthBearerAuthenticationHeaderValueProvider sut = CreateProvider(handler);

        Action act = () => sut.GetAuthenticationHeader();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*determinístico*");
    }

    [Theory(DisplayName = "5xx no token endpoint propaga HttpRequestException com ConnectionError (transient)")]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public void Status5xx_DevePropagarHttpRequestExceptionTransient(HttpStatusCode status)
    {
        using StubHttpMessageHandler handler = new();
        handler.EnqueueResponse(status, "Server error");
        using OAuthBearerAuthenticationHeaderValueProvider sut = CreateProvider(handler);

        Action act = () => sut.GetAuthenticationHeader();

        act.Should().Throw<HttpRequestException>()
            .Which.HttpRequestError.Should().Be(HttpRequestError.ConnectionError,
                because: "5xx é tratado como transient para que SchemaRegistrationHostedService siga o caminho fail-graceful.");
    }

    [Fact(DisplayName = "Renovações concorrentes serializam — 1 chamada só ao token endpoint")]
    public async Task RenovacoesConcorrentes_DeveSerializarChamadas()
    {
        using StubHttpMessageHandler handler = new();
        handler.EnqueueResponse(HttpStatusCode.OK, TokenResponseJson("jwt-shared", 300));
        using OAuthBearerAuthenticationHeaderValueProvider sut = CreateProvider(handler);

        // 10 threads disputam token cold-cache simultaneamente
        Task<AuthenticationHeaderValue>[] tasks = Enumerable
            .Range(0, 10)
            .Select(_ => Task.Run(sut.GetAuthenticationHeader))
            .ToArray();

        AuthenticationHeaderValue[] results = await Task.WhenAll(tasks);

        results.Should().AllSatisfy(h =>
        {
            h.Scheme.Should().Be("Bearer");
            h.Parameter.Should().Be("jwt-shared");
        });
        handler.CallCount.Should().Be(1,
            because: "SemaphoreSlim serializa renovações; threads pós-refresh reutilizam o token recém-obtido.");
    }

    [Fact(DisplayName = "Dispose com ownsHttpClient=true libera SemaphoreSlim — uso subsequente lança")]
    public void Dispose_OwnsHttpClient_DeveLiberarRecursos()
    {
        using StubHttpMessageHandler handler = new();
        OAuthBearerAuthenticationHeaderValueProvider sut = CreateProvider(handler);

        // Sem chamar GetAuthenticationHeader antes — cache permanece nulo. Após Dispose,
        // a próxima chamada vai entrar no caminho de refresh (refreshLock.WaitAsync) que
        // dispara ObjectDisposedException no SemaphoreSlim disposto. Se houvesse token
        // cached, o fluxo retornaria early sem tocar o semaphore (cache hit).
        sut.Dispose();

        Action useAfterDispose = () => _ = sut.GetAuthenticationHeader();
        useAfterDispose.Should().Throw<ObjectDisposedException>(
            because: "SemaphoreSlim disposed bloqueia uso subsequente.");
    }

    [Fact(DisplayName = "Dispose com ownsHttpClient=false NÃO dispõe HttpClient externo")]
    public void Dispose_NotOwnsHttpClient_DevePreservarHttpClient()
    {
        using StubHttpMessageHandler handler = new();
        handler.EnqueueResponse(HttpStatusCode.OK, TokenResponseJson("jwt-1", 300));
        handler.EnqueueResponse(HttpStatusCode.OK, TokenResponseJson("jwt-2", 300));
        using HttpClient externalClient = new(handler, disposeHandler: false);

        using (OAuthBearerAuthenticationHeaderValueProvider sut = new(
            externalClient,
            settings: DefaultSettings(),
            logger: NullLogger<OAuthBearerAuthenticationHeaderValueProvider>.Instance))
        {
            sut.GetAuthenticationHeader();
        }

        // HttpClient externo deve continuar utilizável (managed pelo IHttpClientFactory na realidade).
        Action useExternalAfterProviderDispose = () =>
        {
            // Faz uma request direta — handler está acessível, response 200 OK.
            using HttpResponseMessage _ = externalClient.GetAsync(new Uri("https://kc.test/")).GetAwaiter().GetResult();
        };

        // Se externalClient tivesse sido disposed, lançaria ObjectDisposedException antes do handler.
        useExternalAfterProviderDispose.Should().NotThrow<ObjectDisposedException>();
    }

    [Fact(DisplayName = "Dispose é idempotente — múltiplas chamadas não lançam")]
    public void Dispose_Multiplo_DeveSerIdempotente()
    {
        using StubHttpMessageHandler handler = new();
        OAuthBearerAuthenticationHeaderValueProvider sut = CreateProvider(handler);

        sut.Dispose();
        Action act = sut.Dispose;

        act.Should().NotThrow();
    }
}
