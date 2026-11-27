namespace Unifesspa.UniPlus.Autorizacao.IntegrationTests.Paginacao;

using System.Net;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Paginacao;

using static Unifesspa.UniPlus.Autorizacao.IntegrationTests.Paginacao.ApoioDosCursoresDeAutorizacao;

/// <summary>O cursor da lista de revisões de acessos continua só a situação que o emitiu.</summary>
[Collection(AutorizacaoEndpointCollection.Name)]
public sealed class CursorDasRevisoesDeAcessosTests : IAsyncLifetime
{
    private const string Representante = "marlene.martins";

    private readonly ApoioDosCursoresDeAutorizacao _apoio;

    public CursorDasRevisoesDeAcessosTests(AutorizacaoEndpointFixture fixture) => _apoio = new ApoioDosCursoresDeAutorizacao(fixture);

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => _apoio.RemoverSemeadosAsync();

    [Fact(DisplayName = "Acrescentar a situação ao cursor da lista sem filtro recusa a continuação")]
    public async Task Continuar_SituacaoAcrescentada_Recusa()
    {
        using ProvaDoCursor prova = await ProvaAsync();

        await prova.RecusaTrocaAsync(string.Empty, "situacao", "CONCLUIDA");
    }

    [Fact(DisplayName = "Trocar a situação no cursor da lista recusa a continuação")]
    public async Task Continuar_SituacaoTrocada_Recusa()
    {
        using ProvaDoCursor prova = await ProvaAsync();

        await prova.RecusaTrocaAsync("situacao=CONCLUIDA", "situacao", "EM_ANDAMENTO");
    }

    [Fact(DisplayName = "Situação inválida com o cursor de outra consulta é recusada pelo filtro, não pelo cursor")]
    public async Task Continuar_SituacaoInvalida_RecusaPeloFiltro()
    {
        using ProvaDoCursor prova = await ProvaAsync();

        await prova.RecusaPeloFiltroAsync(
            string.Empty, "situacao", "desconhecida", HttpStatusCode.BadRequest, "uniplus.autorizacao.revisao.filtro_de_situacao_invalido");
    }

    private async Task<ProvaDoCursor> ProvaAsync()
    {
        await _apoio.SemearRevisoesConcluidasAsync(3);
        HttpClient representante = await _apoio.ClienteDeGovernancaAsync(Representante, TipoDeGovernanca.RepresentanteDoControlador);

        return new ProvaDoCursor(new ConsultaPaginada(representante, RotaDasRevisoes), _apoio.Cursores);
    }
}
