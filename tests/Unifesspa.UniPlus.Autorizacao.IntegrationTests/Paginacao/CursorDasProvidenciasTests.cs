namespace Unifesspa.UniPlus.Autorizacao.IntegrationTests.Paginacao;

using System.Net;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Paginacao;

using static Unifesspa.UniPlus.Autorizacao.IntegrationTests.Paginacao.ApoioDosCursoresDeAutorizacao;

/// <summary>O cursor da fila das providências continua só a situação e a concessão que o emitiram.</summary>
[Collection(AutorizacaoEndpointCollection.Name)]
public sealed class CursorDasProvidenciasTests : IAsyncLifetime
{
    private const string Gestor = "diogo.souza";
    private const string OutroIdentificador = "019a0000-0000-7000-8000-0000000000aa";

    private readonly ApoioDosCursoresDeAutorizacao _apoio;

    public CursorDasProvidenciasTests(AutorizacaoEndpointFixture fixture) => _apoio = new ApoioDosCursoresDeAutorizacao(fixture);

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => _apoio.RemoverSemeadosAsync();

    [Theory(DisplayName = "Acrescentar a situação ou a concessão ao cursor da fila sem filtro recusa a continuação")]
    [InlineData("situacao", "ABERTA")]
    [InlineData("concessao", OutroIdentificador)]
    public async Task Continuar_FiltroAcrescentado_Recusa(string parametro, string valor)
    {
        using ProvaDoCursor prova = await ProvaAsync();

        await prova.RecusaTrocaAsync(string.Empty, parametro, valor);
    }

    [Fact(DisplayName = "Trocar a situação no cursor da fila recusa a continuação")]
    public async Task Continuar_SituacaoTrocada_Recusa()
    {
        using ProvaDoCursor prova = await ProvaAsync();

        await prova.RecusaTrocaAsync("situacao=ABERTA", "situacao", "RESPONDIDA");
    }

    [Fact(DisplayName = "Situação inválida com o cursor de outra consulta é recusada pelo filtro, não pelo cursor")]
    public async Task Continuar_SituacaoInvalida_RecusaPeloFiltro()
    {
        using ProvaDoCursor prova = await ProvaAsync();

        await prova.RecusaPeloFiltroAsync(
            string.Empty, "situacao", "desconhecida", HttpStatusCode.BadRequest, "uniplus.autorizacao.providencia.filtro_de_situacao_invalido");
    }

    private async Task<ProvaDoCursor> ProvaAsync()
    {
        await _apoio.SemearProvidenciasAsync(3);
        HttpClient gestor = await _apoio.ClienteDeGovernancaAsync(Gestor, TipoDeGovernanca.GestorDeAcessos);

        return new ProvaDoCursor(new ConsultaPaginada(gestor, RotaDasProvidencias), _apoio.Cursores);
    }
}
