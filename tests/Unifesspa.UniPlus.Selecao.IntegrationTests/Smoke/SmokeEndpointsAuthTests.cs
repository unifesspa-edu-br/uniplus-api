namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Smoke;

using System.Net;
using System.Net.Http.Headers;

using AwesomeAssertions;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Selecao.IntegrationTests.Infrastructure;

/// <summary>
/// Cobertura de autorização dos endpoints smoke E2E (issue #346). Valida o contrato:
/// anônimos recebem 401, autenticados sem role admin recebem 403, e admin avança até o
/// handler. O round-trip completo (Storage/Cache/Messaging com deps reais) é exercitado
/// manualmente no cluster standalone — fora do escopo de testes de integração HTTP-only.
/// </summary>
public sealed class SmokeEndpointsAuthTests : IClassFixture<SelecaoApiFactory>
{
    private readonly SelecaoApiFactory _factory;

    public SmokeEndpointsAuthTests(SelecaoApiFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;
    }

    [Theory]
    [InlineData(HttpMethodName.Post, "/api/_smoke/storage/upload")]
    [InlineData(HttpMethodName.Get, "/api/_smoke/cache/anykey")]
    [InlineData(HttpMethodName.Post, "/api/_smoke/messaging/publish")]
    public async Task SmokeEndpoint_SemAutenticacao_Retorna401(HttpMethodName method, string path)
    {
        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await SendAsync(client, method, path);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(HttpMethodName.Post, "/api/_smoke/storage/upload")]
    [InlineData(HttpMethodName.Get, "/api/_smoke/cache/anykey")]
    [InlineData(HttpMethodName.Post, "/api/_smoke/messaging/publish")]
    public async Task SmokeEndpoint_AutenticadoSemRoleAdmin_Retorna403(HttpMethodName method, string path)
    {
        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme,
            TestAuthHandler.TokenValue);
        // Sem header X-Test-Roles: usuário autenticado mas sem role admin.

        using HttpResponseMessage response = await SendAsync(client, method, path);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData(HttpMethodName.Post, "/api/_smoke/storage/upload")]
    [InlineData(HttpMethodName.Get, "/api/_smoke/cache/anykey")]
    [InlineData(HttpMethodName.Post, "/api/_smoke/messaging/publish")]
    public async Task SmokeEndpoint_AutenticadoComRoleNaoAdmin_Retorna403(HttpMethodName method, string path)
    {
        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme,
            TestAuthHandler.TokenValue);
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, "user,viewer");

        using HttpResponseMessage response = await SendAsync(client, method, path);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SmokeEndpoint_AutenticadoComRoleAdmin_AvancaPastoFilter()
    {
        // Confirma que o filter de admin libera para o handler quando role=admin presente.
        // O handler depende de IStorageService/IConnectionMultiplexer/IMessageBus que
        // não estão configurados neste test factory — esperamos uma falha post-filter
        // (5xx ou erro de DI), distinta de 401/403 que seriam o filter rejeitando.
        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme,
            TestAuthHandler.TokenValue);
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, SmokeAdminRole);

        using HttpResponseMessage response = await client.PostAsync(
            new Uri("/api/_smoke/messaging/publish", UriKind.Relative),
            content: null);

        // 200 (se Wolverine outbox conectou em test PG) ou 5xx/4xx pós-handler.
        // O importante: NÃO 401 ou 403 (auth/authz não bloqueou).
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    private const string SmokeAdminRole = "admin";

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethodName method, string path)
    {
        Uri uri = new(path, UriKind.Relative);
        return method switch
        {
            HttpMethodName.Get => client.GetAsync(uri),
            HttpMethodName.Post => client.PostAsync(uri, content: null),
            _ => throw new ArgumentOutOfRangeException(nameof(method), method, "HTTP method não suportado neste teste."),
        };
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance",
        "CA1515:Consider making public types internal",
        Justification = "xUnit Theory InlineData precisa de tipo acessível ao runner — public é aceitável para enum nested.")]
    public enum HttpMethodName
    {
        Get,
        Post,
    }
}
