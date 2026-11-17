namespace Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Instituicoes;

using System.Net;

using AwesomeAssertions;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Assertions;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Infrastructure;

/// <summary>
/// A atualização administrativa da Instituição pelo host real, com Wolverine e Postgres efêmero: o corpo
/// com o mesmo Id da rota altera a Instituição; o Id divergente e o acesso sem a permissão são recusados antes
/// de qualquer leitura do cadastro.
/// </summary>
[Collection(OrganizacaoEndpointCollection.Name)]
public sealed class InstituicaoAtualizarEndpointTests : IAsyncLifetime
{
    private const string NovoNome = "Instituição de teste renomeada";
    private const string ContaSemConcessao = "diogo.souza";

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

    [Fact(DisplayName = "PUT /api/organizacao/admin/instituicao/{id} com o Id da rota diferente do corpo retorna 400 registrado sem tocar o cadastro")]
    public async Task Atualizar_ComIdDivergente_Retorna400SemTocarOCadastro()
    {
        Instituicao instituicao = await _fixture.SemearInstituicaoAsync();
        ConsultasAoCadastroContadas consultas = new();
        using HttpRequestMessage requisicao = RequisicaoAdministrativa.Criar(
            HttpMethod.Put, $"instituicao/{Guid.NewGuid()}", CorpoDaAtualizacao(instituicao));

        using HttpResponseMessage response = await _fixture.EnviarComCadastroContadoAsync(requisicao, consultas);

        await RecusaDoIdDivergente.ConferirAsync(response);
        consultas.Total.Should().Be(0);
        (await _fixture.ObterInstituicaoAsync(instituicao.Id)).Nome.Should().Be(instituicao.Nome);
    }

    [Fact(DisplayName = "PUT /api/organizacao/admin/instituicao/{id} sem a permissão de manutenção retorna 403 sem tocar o cadastro")]
    public async Task Atualizar_SemPermissao_Retorna403SemTocarOCadastro()
    {
        Instituicao instituicao = await _fixture.SemearInstituicaoAsync();
        ConsultasAoCadastroContadas consultas = new();
        using HttpRequestMessage requisicao = RequisicaoAdministrativa.Criar(
            HttpMethod.Put, $"instituicao/{instituicao.Id}", CorpoDaAtualizacao(instituicao));
        requisicao.Headers.TryAddWithoutValidation(TestAuthHandler.UserIdHeader, ContaSemConcessao);

        using HttpResponseMessage response = await _fixture.EnviarComCadastroContadoAsync(requisicao, consultas);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        consultas.Total.Should().Be(0);
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
