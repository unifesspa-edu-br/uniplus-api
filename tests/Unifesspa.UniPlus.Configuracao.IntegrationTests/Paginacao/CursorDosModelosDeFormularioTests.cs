namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Paginacao;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Paginacao;

using static Unifesspa.UniPlus.Configuracao.IntegrationTests.ModelosFormulario.ModeloFormularioHttp;
using static Unifesspa.UniPlus.Configuracao.IntegrationTests.ModelosFormulario.ModelosParaTeste;

/// <summary>O cursor da lista dos modelos de formulário continua só o tipo de processo, a fase e o estado que o emitiram.</summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class CursorDosModelosDeFormularioTests : IAsyncLifetime
{
    private readonly ApoioDosCursoresDeConfiguracao _apoio;
    private readonly List<Guid> _criados = [];

    public CursorDosModelosDeFormularioTests(ConfiguracaoEndpointFixture fixture) => _apoio = new ApoioDosCursoresDeConfiguracao(fixture);

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => _apoio.RemoverModelosAsync(_criados);

    [Theory(DisplayName = "Acrescentar um filtro ao cursor da lista de modelos recusa a continuação")]
    [InlineData("tipoProcesso", "QUALQUER_TIPO")]
    [InlineData("tipoProcesso", "\u0000")]
    [InlineData("serveAFaseDeInscricao", "false")]
    [InlineData("ativo", "false")]
    public async Task Continuar_FiltroAcrescentado_Recusa(string parametro, string valor)
    {
        using ProvaDoCursor prova = await ProvaAsync();

        await prova.RecusaTrocaAsync(string.Empty, parametro, valor);
    }

    [Theory(DisplayName = "Trocar o valor do filtro no cursor da lista de modelos recusa a continuação")]
    [InlineData("serveAFaseDeInscricao", "false", "true")]
    [InlineData("ativo", "false", "true")]
    [InlineData("tipoProcesso", "QUALQUER_TIPO", "OUTRO_TIPO")]
    [InlineData("tipoProcesso", "QUALQUER_TIPO", "\u0000")]
    public async Task Continuar_FiltroTrocado_Recusa(string parametro, string inicial, string novo)
    {
        using ProvaDoCursor prova = await ProvaAsync();

        await prova.RecusaTrocaAsync($"{parametro}={inicial}", parametro, novo);
    }

    [Fact(DisplayName = "O mesmo filtro dito de novo continua, e o percurso inteiro não pula nem repete modelo")]
    public async Task Continuar_MesmoFiltro_Continua()
    {
        using ProvaDoCursor prova = await ProvaAsync();

        await prova.ContinuaComAsync("ativo=false", "ativo", "false");
        await prova.ConfereOPercursoEquivalenteAsync("ativo=false", 1, static link => LinkDePaginacao.ComParametro(link, "ativo", "false"));
    }

    [Fact(DisplayName = "Cursor sem a chave, de outra coleção, malformado, de outra consulta ou vencido é recusado")]
    public async Task Cursor_Adulterado_Recusa()
    {
        using ProvaDoCursor prova = await ProvaAsync();

        await prova.RecusaCursorAdulteradoAsync("ativo=false");
        await prova.RecusaCursorVencidoAsync("ativo=false");
        await prova.ConferePayloadAsync("tipoProcesso=QUALQUER_TIPO", vinculadoAConta: false, "QUALQUER_TIPO");
    }

    [Fact(DisplayName = "A chave com uma coluna de ordenação a mais, mantido o resumo, é recusada como cursor de outra consulta")]
    public async Task Continuar_ChaveComColunaAMais_Recusa()
    {
        using ProvaDoCursor prova = await ProvaAsync();

        await prova.RecusaChaveComColunaAMaisAsync("ativo=false");
    }

    [Fact(DisplayName = "A conferência vale também no link prev da lista de modelos")]
    public async Task Cursor_LinkAnterior_ConfereAConsulta()
    {
        using ProvaDoCursor prova = await ProvaAsync();

        await prova.ConfereOLinkAnteriorAsync("ativo=false", static link => LinkDePaginacao.ComParametro(link, "ativo", "true"));
    }

    // Os modelos nascem desativados e servem a uma fase que não é a de inscrição, para todos os tipos de processo.
    private async Task<ProvaDoCursor> ProvaAsync()
    {
        using HttpClient cliente = _apoio.Cliente();
        for (int i = 0; i < 3; i++)
        {
            _criados.Add(await CriarAsync(cliente, Habilitacao($"MOD_{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}")));
        }

        return _apoio.Prova(Base);
    }
}
