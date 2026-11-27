namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Paginacao;

using System.Net;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Paginacao;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.IntegrationTests.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

/// <summary>O cursor da lista de motivos de decisão continua só o tipo, a subclassificação e o estado que o emitiram.</summary>
[Collection(CascadingCollection.Name)]
public sealed class CursorDosMotivosDeDecisaoTests : IAsyncLifetime
{
    private const string Rota = "/api/selecao/motivos-de-decisao";
    private const string TipoSemeado = "HOMOLOGACAO_DAS_INSCRICOES";

    private readonly ApoioDosCursoresDeSelecao _apoio;
    private readonly ChamadasDosMotivosDeTeste _motivos;

    public CursorDosMotivosDeDecisaoTests(CascadingFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        _apoio = new ApoioDosCursoresDeSelecao(fixture);
        _motivos = new ChamadasDosMotivosDeTeste(fixture, "CURSOR_DOS_MOTIVOS");
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => _motivos.RemoverSemeadosAsync();

    [Theory(DisplayName = "Acrescentar um filtro ao cursor da lista de motivos recusa a continuação")]
    [InlineData("tipo", "ANALISE_DOCUMENTAL")]
    [InlineData("subclassificacao", "CADASTRO_UNICO")]
    [InlineData("apenasAtivos", "false")]
    public async Task Continuar_FiltroAcrescentado_Recusa(string parametro, string valor)
    {
        using ProvaDoCursor prova = await ProvaAsync();

        await prova.RecusaTrocaAsync($"tipo={TipoSemeado}", parametro, valor);
    }

    [Fact(DisplayName = "Trocar o tipo no cursor da lista de motivos recusa a continuação")]
    public async Task Continuar_TipoTrocado_Recusa()
    {
        using ProvaDoCursor prova = await ProvaAsync();

        await prova.RecusaTrocaAsync($"tipo={TipoSemeado}", "tipo", "ANALISE_DOCUMENTAL");
    }

    [Fact(DisplayName = "Dizer o padrão de que só os ativos vêm é a mesma consulta e continua")]
    public async Task Continuar_PadraoExplicito_Continua()
    {
        using ProvaDoCursor prova = await ProvaAsync();

        await prova.ContinuaComAsync($"tipo={TipoSemeado}", "apenasAtivos", "true");
        await prova.ConfereOPercursoEquivalenteAsync(
            $"tipo={TipoSemeado}", 1, static link => LinkDePaginacao.ComParametro(link, "apenasAtivos", "true"));
    }

    [Theory(DisplayName = "Filtro inválido com o cursor de outra consulta é recusado pelo filtro, não pelo cursor")]
    [InlineData("tipo", "homologacao", "uniplus.selecao.motivo_de_decisao.filtro_de_tipo_invalido")]
    [InlineData("subclassificacao", "cadastro_unico", "uniplus.selecao.motivo_de_decisao.filtro_de_subclassificacao_invalida")]
    public async Task Continuar_FiltroInvalido_RecusaPeloFiltro(string parametro, string valor, string codigo)
    {
        using ProvaDoCursor prova = await ProvaAsync();

        await prova.RecusaPeloFiltroAsync($"tipo={TipoSemeado}", parametro, valor, HttpStatusCode.BadRequest, codigo);
    }

    [Fact(DisplayName = "Cursor sem a chave, de outra coleção, malformado, de outra consulta ou vencido é recusado")]
    public async Task Cursor_Adulterado_Recusa()
    {
        using ProvaDoCursor prova = await ProvaAsync();

        await prova.RecusaCursorAdulteradoAsync($"tipo={TipoSemeado}");
        await prova.RecusaCursorVencidoAsync($"tipo={TipoSemeado}");
        await prova.ConferePayloadAsync($"tipo={TipoSemeado}", vinculadoAConta: false, TipoSemeado);
    }

    [Fact(DisplayName = "A chave com uma coluna de ordenação a mais, mantido o resumo, é recusada como cursor de outra consulta")]
    public async Task Continuar_ChaveComColunaAMais_Recusa()
    {
        using ProvaDoCursor prova = await ProvaAsync();

        await prova.RecusaChaveComColunaAMaisAsync($"tipo={TipoSemeado}");
    }

    [Fact(DisplayName = "A conferência vale também no link prev da lista de motivos")]
    public async Task Cursor_LinkAnterior_ConfereAConsulta()
    {
        using ProvaDoCursor prova = await ProvaAsync();

        await prova.ConfereOLinkAnteriorAsync($"tipo={TipoSemeado}", static link => LinkDePaginacao.ComParametro(link, "apenasAtivos", "false"));
    }

    private async Task<ProvaDoCursor> ProvaAsync()
    {
        for (int i = 0; i < 3; i++)
        {
            await _motivos.SemearMotivoAsync();
        }

        return _apoio.Prova(_apoio.Anonimo(), Rota);
    }
}
