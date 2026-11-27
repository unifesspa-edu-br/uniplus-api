namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Paginacao;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Cursos;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Paginacao;

/// <summary>O cursor da lista de cursos continua só a busca e a ordenação que o emitiram.</summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class CursorDosCursosTests : IAsyncLifetime
{
    private const string Rota = "/api/configuracao/cursos";
    private static readonly string[] Sufixos = ["Alfa", "Beta", "Gama"];

    private readonly ApoioDosCursoresDeConfiguracao _apoio;
    private readonly List<string> _blocos = [];

    public CursorDosCursosTests(ConfiguracaoEndpointFixture fixture) => _apoio = new ApoioDosCursoresDeConfiguracao(fixture);

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        foreach (string bloco in _blocos)
        {
            await _apoio.RemoverCursosDoBlocoAsync(bloco);
        }
    }

    [Fact(DisplayName = "Trocar a busca no cursor da lista de cursos recusa a continuação")]
    public async Task Continuar_BuscaTrocada_Recusa()
    {
        (ProvaDoCursor prova, string bloco) = await ProvaAsync();
        using (prova)
        {
            await prova.RecusaTrocaAsync($"q={bloco}", "q", $"{bloco} Beta");
        }
    }

    [Fact(DisplayName = "Acrescentar a ordenação ao cursor da lista sem ordenação recusa a continuação")]
    public async Task Continuar_OrdenacaoAcrescentada_Recusa()
    {
        (ProvaDoCursor prova, string bloco) = await ProvaAsync();
        using (prova)
        {
            await prova.RecusaTrocaAsync($"q={bloco}", "sort", "-nome");
        }
    }

    [Fact(DisplayName = "Trocar a ordenação no cursor da lista de cursos recusa a continuação")]
    public async Task Continuar_OrdenacaoTrocada_Recusa()
    {
        (ProvaDoCursor prova, string bloco) = await ProvaAsync();
        using (prova)
        {
            await prova.RecusaTrocaAsync($"q={bloco}&sort=nome", "sort", "-nome");
        }
    }

    [Fact(DisplayName = "A busca com espaços nas pontas é a mesma busca e o percurso inteiro não pula nem repete curso")]
    public async Task Continuar_BuscaComEspacosNasPontas_Continua()
    {
        (ProvaDoCursor prova, string bloco) = await ProvaAsync();
        using (prova)
        {
            await prova.ContinuaComAsync($"q={bloco}", "q", $"  {bloco} ");
            await prova.ConfereOPercursoEquivalenteAsync($"q={bloco}", 1, link => LinkDePaginacao.ComParametro(link, "q", $"  {bloco} "));
        }
    }

    // O nome normalizado do curso viaja na chave quando a ordenação é por nome; ordenando pelo instante de criação a chave
    // só leva o instante, e então nada do texto da busca poderia estar nela.
    [Fact(DisplayName = "O cursor da lista de cursos, ordenada pelo instante de criação, não leva o texto da busca nem resumo de conta")]
    public async Task Cursor_Payload_NaoLevaABusca()
    {
        (ProvaDoCursor prova, string bloco) = await ProvaAsync();
        using (prova)
        {
            await prova.ConferePayloadAsync($"q={bloco}&sort=criadoEm", vinculadoAConta: false, bloco);
        }
    }

    [Fact(DisplayName = "Cursor sem a chave, de outra coleção, malformado, de outra consulta ou vencido é recusado")]
    public async Task Cursor_Adulterado_Recusa()
    {
        (ProvaDoCursor prova, string bloco) = await ProvaAsync();
        using (prova)
        {
            await prova.RecusaCursorAdulteradoAsync($"q={bloco}");
            await prova.RecusaCursorVencidoAsync($"q={bloco}");
        }
    }

    [Fact(DisplayName = "A coluna de ordenação que não é um instante é recusada como cursor de outra consulta")]
    public async Task Continuar_ColunaComInstanteInvalido_Recusa()
    {
        (ProvaDoCursor prova, string bloco) = await ProvaAsync();
        using (prova)
        {
            await prova.RecusaColunaComCodigoDesconhecidoAsync($"q={bloco}&sort=criadoEm", 0, "não é um instante");
        }
    }

    [Fact(DisplayName = "A conferência vale também no link prev da lista de cursos")]
    public async Task Cursor_LinkAnterior_ConfereAConsulta()
    {
        (ProvaDoCursor prova, string bloco) = await ProvaAsync();
        using (prova)
        {
            await prova.ConfereOLinkAnteriorAsync($"q={bloco}", static link => LinkDePaginacao.ComParametro(link, "sort", "-nome"));
        }
    }

    private async Task<(ProvaDoCursor Prova, string Bloco)> ProvaAsync()
    {
        string bloco = ApoioDaOrdenacaoAlfabetica.Bloco();
        _blocos.Add(bloco);
        using HttpClient cliente = _apoio.Cliente();
        await ApoioDaOrdenacaoAlfabetica.CriarCursosAsync(cliente, bloco, Sufixos);

        return (_apoio.Prova(Rota), bloco);
    }
}
