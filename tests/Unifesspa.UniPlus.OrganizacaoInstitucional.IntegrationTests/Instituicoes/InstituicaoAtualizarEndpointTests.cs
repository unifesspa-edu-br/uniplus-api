namespace Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Instituicoes;

using System.Net;

using AwesomeAssertions;

using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Infrastructure;

/// <summary>
/// A atualização administrativa da Instituição pelo host real, com Wolverine e Postgres efêmero: o corpo
/// com o mesmo Id da rota altera a Instituição.
/// </summary>
[Collection(OrganizacaoEndpointCollection.Name)]
public sealed class InstituicaoAtualizarEndpointTests : IAsyncLifetime
{
    private const string NovoNome = "Instituição de teste renomeada";

    private readonly OrganizacaoEndpointFixture _fixture;

    public InstituicaoAtualizarEndpointTests(OrganizacaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.RemoverInstituicoesAsync();

    public Task DisposeAsync() => _fixture.RemoverInstituicoesAsync();

    [Fact(DisplayName = "PUT /api/organizacao/admin/instituicao/{id} com o mesmo Id no corpo retorna 204 e grava o novo nome")]
    public async Task Atualizar_ComIdIgualAoDoCorpo_Retorna204EGravaONome()
    {
        Instituicao instituicao = await _fixture.SemearInstituicaoAsync();
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage requisicao = RequisicaoAdministrativa.Criar(
            HttpMethod.Put, $"instituicao/{instituicao.Id}", CorpoDaAtualizacao(instituicao));

        using HttpResponseMessage response = await client.SendAsync(requisicao);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        Instituicao gravada = await _fixture.ObterInstituicaoAsync(instituicao.Id);
        gravada.Nome.Should().Be(NovoNome);
    }

    private static object CorpoDaAtualizacao(Instituicao instituicao) =>
        new
        {
            id = instituicao.Id,
            codigoEmec = instituicao.CodigoEmec,
            nome = NovoNome,
            sigla = instituicao.Sigla,
            organizacaoAcademica = instituicao.OrganizacaoAcademica,
            categoriaAdministrativa = instituicao.CategoriaAdministrativa,
        };
}
