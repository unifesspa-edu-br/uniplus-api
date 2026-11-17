namespace Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Unidades;

using System.Net;

using AwesomeAssertions;

using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Infrastructure;

/// <summary>
/// A atualização administrativa da Unidade pelo host real, com Wolverine e Postgres efêmero: o corpo com o
/// mesmo Id da rota altera a Unidade.
/// </summary>
[Collection(OrganizacaoEndpointCollection.Name)]
public sealed class UnidadeAtualizarEndpointTests
{
    private const string NovoNome = "Unidade de teste renomeada";

    private readonly OrganizacaoEndpointFixture _fixture;

    public UnidadeAtualizarEndpointTests(OrganizacaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "PUT /api/organizacao/admin/unidades/{id} com o mesmo Id no corpo retorna 204 e grava o novo nome")]
    public async Task Atualizar_ComIdIgualAoDoCorpo_Retorna204EGravaONome()
    {
        Unidade unidade = await _fixture.SemearUnidadeAsync();
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage requisicao = RequisicaoAdministrativa.Criar(
            HttpMethod.Put, $"unidades/{unidade.Id}", CorpoDaAtualizacao(unidade));

        using HttpResponseMessage response = await client.SendAsync(requisicao);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        Unidade gravada = await _fixture.ObterUnidadeAsync(unidade.Id);
        gravada.Nome.Should().Be(NovoNome);
    }

    private static object CorpoDaAtualizacao(Unidade unidade) =>
        new
        {
            id = unidade.Id,
            nome = NovoNome,
            slug = unidade.Slug.Valor,
            sigla = unidade.Sigla,
            codigo = unidade.Codigo,
            tipo = (int)unidade.Tipo,
            unidadeAcademica = unidade.UnidadeAcademica,
        };
}
