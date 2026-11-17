namespace Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Unidades;

using System.Net;
using System.Text;

using AwesomeAssertions;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Assertions;
using Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Infrastructure;

/// <summary>
/// Os endpoints de <c>Unidade</c> pelo host real, com Wolverine e Postgres efêmero: o 404 registrado da leitura
/// por Id e a autenticação e a idempotência das rotas administrativas. A listagem, a leitura e a atualização
/// têm classe própria.
/// </summary>
[Collection(OrganizacaoEndpointCollection.Name)]
public sealed class UnidadeEndpointTests
{
    private readonly OrganizacaoEndpointFixture _fixture;

    public UnidadeEndpointTests(OrganizacaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "GET /api/organizacao/unidades/{id} retorna 404 registrado quando a unidade é inexistente")]
    public async Task ObterPorId_NaoExiste_Retorna404Registrado()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(
            new Uri($"/api/organizacao/unidades/{Guid.NewGuid()}", UriKind.Relative));

        await ProblemaRegistrado.ConferirAsync(
            response,
            HttpStatusCode.NotFound,
            "uniplus.organizacao.unidade.nao_encontrada",
            "Unidade não encontrada",
            "Unidade não encontrada.");
    }

    [Fact(DisplayName = "POST /api/organizacao/admin/unidades sem Idempotency-Key retorna 400")]
    public async Task Criar_SemIdempotencyKey_Retorna400()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = RequisicaoAdministrativa.Criar(HttpMethod.Post, "unidades", new { });
        request.Headers.Remove("Idempotency-Key");

        using HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "Idempotency-Key é obrigatório — sem ela o filtro retorna 400 antes do action");
    }

    [Fact(DisplayName = "POST /api/organizacao/admin/unidades sem autenticação retorna 401")]
    public async Task Criar_SemAuth_Retorna401()
    {
        using HttpClient client = _fixture.Factory.CreateDefaultClient();
        using HttpRequestMessage request = new(
            HttpMethod.Post,
            new Uri("/api/organizacao/admin/unidades", UriKind.Relative));
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        using HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "DELETE /api/organizacao/admin/unidades/{id} sem autenticação retorna 401")]
    public async Task Remover_SemAuth_Retorna401()
    {
        using HttpClient client = _fixture.Factory.CreateDefaultClient();

        using HttpResponseMessage response = await client.DeleteAsync(
            new Uri($"/api/organizacao/admin/unidades/{Guid.NewGuid()}", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
