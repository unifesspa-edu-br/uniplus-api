namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.FatosCandidato;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

/// <summary>
/// Smoke dos endpoints somente-leitura do catálogo <c>rol_de_fatos_candidato</c>
/// (UNI-REQ-0077, ADR-0111) com Wolverine contra Postgres efêmero: routing, vendor
/// media type, resolução do leitor cross-módulo via DI ponta-a-ponta, e 404 por
/// chave natural inexistente.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class FatoCandidatoEndpointTests
{
    private readonly ConfiguracaoEndpointFixture _fixture;

    public FatoCandidatoEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "GET /api/configuracao/fatos-candidato retorna 200 com vendor MIME e os nove fatos")]
    public async Task Listar_Retorna200ComOsNoveFatos()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri("/api/configuracao/fatos-candidato", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType
            .Should().Be("application/vnd.uniplus.fato-candidato.v1+json");

        List<FatoCandidatoView>? fatos = await response.Content.ReadFromJsonAsync<List<FatoCandidatoView>>();
        fatos.Should().NotBeNull();
        fatos!.Should().HaveCount(11);
        fatos.Select(f => f.Codigo).Should().BeInAscendingOrder(StringComparer.Ordinal);
        fatos.Should().Contain(f => f.Codigo == "MODALIDADE" && f.Cardinalidade == "MULTIVALORADO" && f.ValoresDominio == null);
    }

    [Fact(DisplayName = "GET /api/configuracao/fatos-candidato/{codigo} resolve pela chave natural")]
    public async Task ObterPorCodigo_Resolve()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri("/api/configuracao/fatos-candidato/COR_RACA", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        FatoCandidatoView? fato = await response.Content.ReadFromJsonAsync<FatoCandidatoView>();
        fato.Should().NotBeNull();
        fato!.Codigo.Should().Be("COR_RACA");
        fato.Dominio.Should().Be("CATEGORICO");
        fato.ValoresDominio.Should().BeNull("COR_RACA migrou o conjunto fechado para ValoresDominioDeclarados (ADR-0116)");
        fato.ValoresDominioDeclarados.Should().NotBeNull().And.Contain(v => v.Codigo == "PRETA");
    }

    [Fact(DisplayName = "GET /api/configuracao/fatos-candidato/{codigo} retorna 404 para código inexistente")]
    public async Task ObterPorCodigo_Inexistente_Retorna404()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri("/api/configuracao/fatos-candidato/NAO_EXISTE", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
