namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Paginacao;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Paginacao;

using static Unifesspa.UniPlus.Configuracao.IntegrationTests.TiposProcesso.RequisicoesDeTipoProcesso;

/// <summary>O cursor da lista de manutenção dos tipos de processo continua só o recorte dos ativos que o emitiu.</summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class CursorDosTiposDeProcessoTests : IAsyncLifetime
{
    private const string Rota = "/api/configuracao/admin/tipos-processo";

    private readonly ApoioDosCursoresDeConfiguracao _apoio;
    private readonly List<Guid> _criados = [];

    public CursorDosTiposDeProcessoTests(ConfiguracaoEndpointFixture fixture) => _apoio = new ApoioDosCursoresDeConfiguracao(fixture);

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => _apoio.RemoverTiposDeProcessoAsync(_criados);

    [Fact(DisplayName = "Acrescentar ou trocar o recorte dos ativos no cursor da lista recusa a continuação")]
    public async Task Continuar_RecorteDosAtivosAlterado_Recusa()
    {
        using ProvaDoCursor prova = await ProvaAsync();

        await prova.RecusaTrocaAsync(string.Empty, "apenasAtivos", "true");
        await prova.RecusaTrocaAsync("apenasAtivos=true", "apenasAtivos", "false");
    }

    [Fact(DisplayName = "Dizer o padrão de que a lista traz todos os tipos é a mesma consulta e continua")]
    public async Task Continuar_PadraoExplicito_Continua()
    {
        using ProvaDoCursor prova = await ProvaAsync();

        await prova.ContinuaComAsync(string.Empty, "apenasAtivos", "false");
        await prova.ConfereOPercursoEquivalenteAsync(string.Empty, 1, static link => LinkDePaginacao.ComParametro(link, "apenasAtivos", "false"));
    }

    [Fact(DisplayName = "Cursor sem a chave, de outra coleção, malformado, de outra consulta ou vencido é recusado")]
    public async Task Cursor_Adulterado_Recusa()
    {
        using ProvaDoCursor prova = await ProvaAsync();

        await prova.RecusaCursorAdulteradoAsync(string.Empty);
        await prova.RecusaCursorVencidoAsync(string.Empty);
        await prova.ConferePayloadAsync(string.Empty, vinculadoAConta: false);
    }

    [Fact(DisplayName = "A conferência vale também no link prev da lista de tipos de processo")]
    public async Task Cursor_LinkAnterior_ConfereAConsulta()
    {
        using ProvaDoCursor prova = await ProvaAsync();

        await prova.ConfereOLinkAnteriorAsync(string.Empty, static link => LinkDePaginacao.ComParametro(link, "apenasAtivos", "true"));
    }

    private async Task<ProvaDoCursor> ProvaAsync()
    {
        using HttpClient cliente = _apoio.Cliente();
        for (int i = 0; i < 3; i++)
        {
            _criados.Add(await CriarTipoInicial(cliente, CodigoUnico()));
        }

        return _apoio.Prova(Rota);
    }
}
