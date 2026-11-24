namespace Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Unidades;

using System.Net;

using AwesomeAssertions;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Assertions;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Infrastructure;

/// <summary>
/// A atualização administrativa da Unidade pelo host real, com Wolverine e Postgres efêmero: o corpo com o
/// mesmo Id da rota altera a Unidade; o Id divergente e o acesso sem a permissão são recusados antes de
/// qualquer leitura do cadastro.
/// </summary>
[Collection(OrganizacaoEndpointCollection.Name)]
public sealed class UnidadeAtualizarEndpointTests
{
    private const string NovoNome = "Unidade de teste renomeada";
    private const string ContaSemConcessao = "diogo.souza";

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

    [Fact(DisplayName = "PUT /api/organizacao/admin/unidades/{id} com o Id da rota diferente do corpo retorna 400 registrado sem tocar o cadastro")]
    public async Task Atualizar_ComIdDivergente_Retorna400SemTocarOCadastro()
    {
        Unidade unidade = await _fixture.SemearUnidadeAsync();
        ConsultasAoCadastroContadas consultas = new();
        using HttpRequestMessage requisicao = RequisicaoAdministrativa.Criar(
            HttpMethod.Put, $"unidades/{Guid.NewGuid()}", CorpoDaAtualizacao(unidade));

        using HttpResponseMessage response = await _fixture.EnviarComCadastroContadoAsync(requisicao, consultas);

        await RecusaDoIdDivergente.ConferirAsync(response);
        consultas.Total.Should().Be(0);
        (await _fixture.ObterUnidadeAsync(unidade.Id)).Nome.Should().Be(unidade.Nome);
    }

    [Fact(DisplayName = "PUT /api/organizacao/admin/unidades/{id} sem a permissão de manutenção retorna 403 sem tocar o cadastro")]
    public async Task Atualizar_SemPermissao_Retorna403SemTocarOCadastro()
    {
        Unidade unidade = await _fixture.SemearUnidadeAsync();
        ConsultasAoCadastroContadas consultas = new();
        using HttpRequestMessage requisicao = RequisicaoAdministrativa.Criar(
            HttpMethod.Put, $"unidades/{unidade.Id}", CorpoDaAtualizacao(unidade));
        requisicao.Headers.TryAddWithoutValidation(TestAuthHandler.UserIdHeader, ContaSemConcessao);

        using HttpResponseMessage response = await _fixture.EnviarComCadastroContadoAsync(requisicao, consultas);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        consultas.Total.Should().Be(0);
    }

    private static object CorpoDaAtualizacao(Unidade unidade) =>
        new
        {
            id = unidade.Id,
            nome = NovoNome,
            slug = unidade.Slug.Valor,
            sigla = unidade.Sigla,
            codigo = unidade.Codigo,
            tipo = unidade.Tipo.Codigo(),
            unidadeAcademica = unidade.UnidadeAcademica,
        };
}
