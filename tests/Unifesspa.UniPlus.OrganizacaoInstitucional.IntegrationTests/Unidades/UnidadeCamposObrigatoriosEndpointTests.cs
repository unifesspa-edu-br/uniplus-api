namespace Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Unidades;

using System.Net;

using AwesomeAssertions;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Assertions;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Infrastructure;

/// <summary>
/// A criação e a atualização administrativa da Unidade pelo host real recusam, no envelope da ADR-0023, o indicador de
/// unidade acadêmica e o início da vigência omitidos ou nulos com 422 e o código genérico de obrigatório, em vez de
/// gravar <c>false</c> ou o ano 1 em silêncio.
/// </summary>
[Collection(OrganizacaoEndpointCollection.Name)]
public sealed class UnidadeCamposObrigatoriosEndpointTests
{
    private readonly OrganizacaoEndpointFixture _fixture;

    public UnidadeCamposObrigatoriosEndpointTests(OrganizacaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory(DisplayName = "POST /api/organizacao/admin/unidades sem um campo obrigatório, ou com ele nulo, retorna 422 que o nomeia")]
    [InlineData("unidadeAcademica", false)]
    [InlineData("vigenciaInicio", false)]
    [InlineData("unidadeAcademica", true)]
    [InlineData("vigenciaInicio", true)]
    public async Task Criar_SemCampoObrigatorio_Retorna422(string campo, bool nulo)
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage requisicao = RequisicaoAdministrativa.Criar(HttpMethod.Post, "unidades", CorpoDaCriacao(campo, nulo));

        using HttpResponseMessage response = await client.SendAsync(requisicao);

        await RecusaDeCampoObrigatorioNoDominio.ConferirAsync(response, campo);
    }

    [Fact(DisplayName = "POST /api/organizacao/admin/unidades sem os dois campos acumula as duas recusas, na ordem do domínio")]
    public async Task Criar_SemOsDoisCampos_AcumulaAsDuasRecusas()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage requisicao = RequisicaoAdministrativa.Criar(
            HttpMethod.Post, "unidades", new { nome = "Unidade de teste", slug = $"u-{Guid.NewGuid():N}"[..20], sigla = "UT", codigo = "UT01", tipo = "CENTRO" });

        using HttpResponseMessage response = await client.SendAsync(requisicao);

        await RecusaDeCampoObrigatorioNoDominio.ConferirCamposAsync(response, "unidadeAcademica", "vigenciaInicio");
    }

    [Theory(DisplayName = "PUT /api/organizacao/admin/unidades/{id} sem o indicador, ou com ele nulo, retorna 422 e não grava false")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Atualizar_SemUnidadeAcademica_Retorna422ENaoGrava(bool nulo)
    {
        Unidade unidade = await _fixture.SemearUnidadeAsync();
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage requisicao = RequisicaoAdministrativa.Criar(
            HttpMethod.Put, $"unidades/{unidade.Id}", CorpoDaAtualizacao(unidade, nulo));

        using HttpResponseMessage response = await client.SendAsync(requisicao);

        await RecusaDeCampoObrigatorioNoDominio.ConferirAsync(response, "unidadeAcademica");
        (await _fixture.ObterUnidadeAsync(unidade.Id)).UnidadeAcademica.Should().Be(unidade.UnidadeAcademica);
    }

    private static Dictionary<string, object?> CorpoDaCriacao(string campoAusente, bool nulo) =>
        Ausentar(CorpoCompletoDaCriacao(), campoAusente, nulo);

    private static Dictionary<string, object?> CorpoDaAtualizacao(Unidade unidade, bool nulo) =>
        Ausentar(CorpoCompletoDaAtualizacao(unidade), "unidadeAcademica", nulo);

    private static Dictionary<string, object?> CorpoCompletoDaCriacao() =>
        new(StringComparer.Ordinal)
        {
            ["nome"] = "Unidade de teste",
            ["slug"] = $"u-{Guid.NewGuid():N}"[..20],
            ["sigla"] = $"S{Guid.NewGuid():N}"[..8],
            ["codigo"] = $"C{Guid.NewGuid():N}"[..10],
            ["tipo"] = "CENTRO",
            ["unidadeAcademica"] = false,
            ["vigenciaInicio"] = "2026-01-01",
        };

    private static Dictionary<string, object?> CorpoCompletoDaAtualizacao(Unidade unidade) =>
        new(StringComparer.Ordinal)
        {
            ["id"] = unidade.Id,
            ["nome"] = "Unidade de teste renomeada",
            ["slug"] = unidade.Slug.Valor,
            ["sigla"] = unidade.Sigla,
            ["codigo"] = unidade.Codigo,
            ["tipo"] = unidade.Tipo.Codigo(),
            ["unidadeAcademica"] = unidade.UnidadeAcademica,
        };

    private static Dictionary<string, object?> Ausentar(Dictionary<string, object?> corpo, string campo, bool nulo)
    {
        if (nulo)
        {
            corpo[campo] = null;
        }
        else
        {
            corpo.Remove(campo);
        }

        return corpo;
    }
}
