namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Paginacao;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Cursos;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Paginacao;

/// <summary>
/// O cursor da lista de ofertas de curso continua só o curso, a busca e a ordenação que o emitiram, e a coluna de
/// ordenação que viaja no cursor só volta ao valor do contrato.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class CursorDasOfertasDeCursoTests : IAsyncLifetime
{
    private const string Rota = "/api/configuracao/ofertas-curso";
    private const string CodigoNaoPublicado = "PROUNI";

    private readonly ApoioDosCursoresDeConfiguracao _apoio;
    private readonly ApoioDaOrdenacaoAlfabetica _semeadura;
    private readonly List<string> _blocos = [];

    public CursorDasOfertasDeCursoTests(ConfiguracaoEndpointFixture fixture)
    {
        _apoio = new ApoioDosCursoresDeConfiguracao(fixture);
        _semeadura = new ApoioDaOrdenacaoAlfabetica(fixture);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        foreach (string bloco in _blocos)
        {
            await _apoio.RemoverCursosDoBlocoAsync(bloco);
        }
    }

    [Fact(DisplayName = "Acrescentar o curso ao cursor da lista de ofertas recusa a continuação")]
    public async Task Continuar_CursoAcrescentado_Recusa()
    {
        (ProvaDoCursor prova, Guid curso, string nome) = await ProvaAsync();
        using (prova)
        {
            await prova.RecusaTrocaAsync($"q={nome}", "cursoId", curso.ToString());
        }
    }

    [Fact(DisplayName = "Trocar o curso no cursor da lista de ofertas recusa a continuação")]
    public async Task Continuar_CursoTrocado_Recusa()
    {
        (ProvaDoCursor prova, Guid curso, _) = await ProvaAsync();
        using (prova)
        {
            await prova.RecusaTrocaAsync($"cursoId={curso}", "cursoId", Guid.CreateVersion7().ToString());
        }
    }

    [Fact(DisplayName = "Trocar a busca no cursor da lista de ofertas recusa a continuação")]
    public async Task Continuar_BuscaTrocada_Recusa()
    {
        (ProvaDoCursor prova, _, string nome) = await ProvaAsync();
        using (prova)
        {
            await prova.RecusaTrocaAsync($"q={nome}", "q", $"{nome} Outro");
        }
    }

    [Fact(DisplayName = "Acrescentar ou trocar a ordenação no cursor da lista de ofertas recusa a continuação")]
    public async Task Continuar_OrdenacaoAlterada_Recusa()
    {
        (ProvaDoCursor prova, _, string nome) = await ProvaAsync();
        using (prova)
        {
            await prova.RecusaTrocaAsync($"q={nome}", "sort", "programaDeOferta");
            await prova.RecusaTrocaAsync($"q={nome}&sort=programaDeOferta", "sort", "-programaDeOferta");
        }
    }

    [Fact(DisplayName = "A busca com espaços nas pontas é a mesma busca, e o total pedido não muda a consulta")]
    public async Task Continuar_EquivalentesDaConsulta_Continua()
    {
        (ProvaDoCursor prova, _, string nome) = await ProvaAsync();
        using (prova)
        {
            await prova.ContinuaComAsync($"q={nome}", "q", $"  {nome} ");
            await prova.ContinuaComAsync($"q={nome}", "include_total", "true");
        }
    }

    [Theory(DisplayName = "A coluna de ordenação com código que o contrato não publica é recusada como cursor de outra consulta")]
    [InlineData("programaDeOferta")]
    [InlineData("formatoPedagogico")]
    [InlineData("regimeDeFuncionamento")]
    [InlineData("regimeDeTurno")]
    public async Task Continuar_ColunaComCodigoDesconhecido_Recusa(string campo)
    {
        (ProvaDoCursor prova, _, string nome) = await ProvaAsync();
        using (prova)
        {
            await prova.RecusaColunaComCodigoDesconhecidoAsync($"q={nome}&sort={campo}", 0, CodigoNaoPublicado);
        }
    }

    // O nome normalizado do curso viaja na chave quando a ordenação é por ele; ordenando pelo instante de criação a chave só
    // leva o instante, e então nada do texto da busca poderia estar nela.
    [Fact(DisplayName = "O cursor da lista de ofertas, ordenada pelo instante de criação, não leva o texto da busca; o vencido responde 410")]
    public async Task Cursor_PayloadEVencimento()
    {
        (ProvaDoCursor prova, _, string nome) = await ProvaAsync();
        using (prova)
        {
            await prova.ConferePayloadAsync($"q={nome}&sort=criadoEm", vinculadoAConta: false, nome);
            await prova.RecusaCursorVencidoAsync($"q={nome}");
            await prova.RecusaCursorAdulteradoAsync($"q={nome}");
        }
    }

    private async Task<(ProvaDoCursor Prova, Guid Curso, string Nome)> ProvaAsync()
    {
        string nome = ApoioDaOrdenacaoAlfabetica.Bloco();
        _blocos.Add(nome);
        Guid curso = await _semeadura.SemearCursoAsync(nome);
        for (int i = 0; i < 3; i++)
        {
            await _semeadura.SemearOfertaDeAsync(curso, await _semeadura.SemearLocalOfertaAsync());
        }

        return (_apoio.Prova(Rota), curso, nome);
    }
}
