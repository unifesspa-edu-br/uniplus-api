namespace Unifesspa.UniPlus.Publicacoes.IntegrationTests.Paginacao;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Paginacao;
using Unifesspa.UniPlus.Publicacoes.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Publicacoes.IntegrationTests.TiposAtoPublicado;

using static Unifesspa.UniPlus.Publicacoes.IntegrationTests.TiposAtoPublicado.TipoAtoPublicadoApoio;

/// <summary>O cursor da lista pública de tipos de ato continua só o recorte das versões vigentes que o emitiu.</summary>
[Collection(PublicacoesEndpointCollection.Name)]
public sealed class CursorDosTiposDeAtoTests : IAsyncLifetime
{
    private readonly PublicacoesEndpointFixture _fixture;
    private readonly TipoAtoPublicadoApoio _apoio;
    private readonly List<string> _codigos = [];

    public CursorDosTiposDeAtoTests(PublicacoesEndpointFixture fixture)
    {
        _fixture = fixture;
        _apoio = new TipoAtoPublicadoApoio(fixture);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => _apoio.RemoverPorCodigosAsync(_codigos);

    [Fact(DisplayName = "Acrescentar ou trocar o recorte das versões vigentes no cursor da lista recusa a continuação")]
    public async Task Continuar_RecorteAlterado_Recusa()
    {
        using ProvaDoCursor prova = await ProvaAsync();

        await prova.RecusaTrocaAsync(string.Empty, "vigentes", "false");
        await prova.RecusaTrocaAsync("vigentes=false", "vigentes", "true");
    }

    [Fact(DisplayName = "Dizer o padrão das versões vigentes é a mesma consulta e continua")]
    public async Task Continuar_PadraoExplicito_Continua()
    {
        using ProvaDoCursor prova = await ProvaAsync();

        await prova.ContinuaComAsync(string.Empty, "vigentes", "true");
        await prova.ConfereOPercursoEquivalenteAsync(string.Empty, 1, static link => LinkDePaginacao.ComParametro(link, "vigentes", "true"));
    }

    [Fact(DisplayName = "Cursor sem a chave, de outra coleção, malformado, de outra consulta ou vencido é recusado")]
    public async Task Cursor_Adulterado_Recusa()
    {
        using ProvaDoCursor prova = await ProvaAsync();

        await prova.RecusaCursorAdulteradoAsync(string.Empty);
        await prova.RecusaCursorVencidoAsync(string.Empty);
        await prova.ConferePayloadAsync(string.Empty, vinculadoAConta: false);
    }

    [Fact(DisplayName = "A conferência vale também no link prev da lista de tipos de ato")]
    public async Task Cursor_LinkAnterior_ConfereAConsulta()
    {
        using ProvaDoCursor prova = await ProvaAsync();

        await prova.ConfereOLinkAnteriorAsync(string.Empty, static link => LinkDePaginacao.ComParametro(link, "vigentes", "false"));
    }

    private async Task<ProvaDoCursor> ProvaAsync()
    {
        for (int i = 0; i < 3; i++)
        {
            string codigo = CodigoUnico();
            _codigos.Add(codigo);
            await _apoio.CriarAsync(codigo);
        }

        return new ProvaDoCursor(
            new ConsultaPaginada(_fixture.Factory.CreateClient(), Base), new CursoresDeTeste(_fixture.Factory.Services));
    }
}
