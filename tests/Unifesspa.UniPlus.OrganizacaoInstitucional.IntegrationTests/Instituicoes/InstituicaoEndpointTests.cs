namespace Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Instituicoes;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text;

using AwesomeAssertions;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Assertions;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Infrastructure;

/// <summary>
/// Smoke tests dos endpoints da <c>Instituicao</c> singleton (Story #585).
/// Verificam routing, autenticação/autorização e o 404 registrado do GET quando nenhuma
/// Instituição foi cadastrada, com Wolverine rodando contra Postgres efêmero
/// (<see cref="OrganizacaoEndpointFixture"/>).
/// </summary>
[Collection(OrganizacaoEndpointCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class InstituicaoEndpointTests : IAsyncLifetime
{
    private readonly OrganizacaoEndpointFixture _fixture;

    public InstituicaoEndpointTests(OrganizacaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.RemoverInstituicoesAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact(DisplayName = "GET /api/organizacao/instituicao retorna 404 registrado quando nenhuma Instituição cadastrada")]
    public async Task Obter_SemInstituicao_Retorna404Registrado()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(
            new Uri("/api/organizacao/instituicao", UriKind.Relative));

        await ProblemaRegistrado.ConferirAsync(
            response,
            HttpStatusCode.NotFound,
            "uniplus.organizacao.instituicao.nao_encontrada",
            "Instituição não encontrada",
            "Instituição não encontrada.");
    }

    [Fact(DisplayName = "POST /api/organizacao/admin/instituicao sem Idempotency-Key retorna 400")]
    public async Task Criar_SemIdempotencyKey_Retorna400()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(
            HttpMethod.Post,
            new Uri("/api/organizacao/admin/instituicao", UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "Idempotency-Key é obrigatório — sem ela o filtro retorna 400 antes do action");
    }

    [Fact(DisplayName = "POST /api/organizacao/admin/instituicao sem autenticação retorna 401")]
    public async Task Criar_SemAuth_Retorna401()
    {
        using HttpClient client = _fixture.Factory.CreateDefaultClient();
        using HttpRequestMessage request = new(
            HttpMethod.Post,
            new Uri("/api/organizacao/admin/instituicao", UriKind.Relative));
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "PUT /api/organizacao/admin/instituicao/{id} sem autenticação retorna 401")]
    public async Task Atualizar_SemAuth_Retorna401()
    {
        using HttpClient client = _fixture.Factory.CreateDefaultClient();
        using HttpRequestMessage request = new(
            HttpMethod.Put,
            new Uri($"/api/organizacao/admin/instituicao/{Guid.NewGuid()}", UriKind.Relative));
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "DELETE /api/organizacao/admin/instituicao/{id} sem autenticação retorna 401")]
    public async Task Remover_SemAuth_Retorna401()
    {
        using HttpClient client = _fixture.Factory.CreateDefaultClient();

        HttpResponseMessage response = await client.DeleteAsync(
            new Uri($"/api/organizacao/admin/instituicao/{Guid.NewGuid()}", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
