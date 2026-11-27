namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Pagination;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Apoio.ControllersDeTeste;

public sealed class FiltrosRepetidosNoLinkTests
{
    private const string Next = "rel=\"next\"";

    [Fact(DisplayName = "Valor repetido do mesmo parâmetro de busca não leva o texto longo ao link")]
    public async Task Link_BuscaRepetidaComTextoLongo_RepeteSoOPrimeiroValor()
    {
        string longo = new('x', 5000);

        string link = await LinksDaListagemDeTeste.ComProximaPaginaAsync($"?q=ok&q={longo}");

        link.Should().Contain("&q=ok>; rel=\"next\"").And.NotContain(longo);
        link.Length.Should().BeLessThan(new CursorPaginationOptions().ComprimentoMaximoDoCabecalhoLink);
    }

    [Fact(DisplayName = "Parâmetro que a listagem não declara não aparece em nenhum link")]
    public async Task Link_ParametroNaoDeclarado_NaoApareceEmNenhumLink()
    {
        string link = await LinksDaListagemDeTeste.ComProximaPaginaAsync("?parametro-qualquer=valor&q=ok");

        link.Should().NotContain("parametro-qualquer").And.Contain("q=ok");
        (await LinksDaListagemDeTeste.ComPaginaAnteriorAsync("?parametro-qualquer=valor")).Should().NotContain("parametro-qualquer");
    }

    [Fact(DisplayName = "Nome de parâmetro muito longo não é repetido")]
    public async Task Link_NomeDeParametroMuitoLongo_NaoERepetido()
    {
        string nome = new('n', 2000);

        string link = await LinksDaListagemDeTeste.ComProximaPaginaAsync($"?{nome}=valor");

        link.Should().NotContain(nome);
    }

    [Theory(DisplayName = "Cursor e direção só com espaços não vão ao self")]
    [InlineData("?cursor=%20%20%20&direction=%20%20%20")]
    [InlineData("?cursor=&direction=")]
    public async Task Self_CursorEDirecaoEmBranco_NaoSaoRepetidos(string consulta)
    {
        string link = await LinksDaListagemDeTeste.SemProximaPaginaAsync(consulta);

        link.Should().Be($"<{LinksDaListagemDeTeste.EnderecoDaListagem}>; rel=\"self\"");
    }

    [Fact(DisplayName = "O self usa o primeiro valor de direction e de limit, como a leitura da página")]
    public async Task Self_ParametrosDePaginacaoRepetidos_UsaOPrimeiroValor()
    {
        string link = await LinksDaListagemDeTeste.SemProximaPaginaAsync("?limit=10&limit=50&direction=next&direction=prev");

        link.Should().Contain("?limit=10&direction=next>; rel=\"self\"");
    }

    [Fact(DisplayName = "Listagem que não declara q não devolve q")]
    public async Task Link_ListagemSemBusca_NaoDevolveOTermo()
    {
        string link = await LinksDaListagemDeTeste.ComProximaPaginaAsync(
            "?q=termo-ficticio", nameof(CursorDeTesteController.PublicEditais));

        link.Should().NotContain("termo-ficticio").And.NotContain("q=");
    }

    [Fact(DisplayName = "Filtro declarado se mantém no link da próxima página e da anterior")]
    public async Task Link_FiltroDeclarado_MantemNaNavegacao()
    {
        (await LinksDaListagemDeTeste.ComProximaPaginaAsync("?tipoProcesso=SISU")).Should().Contain("&tipoProcesso=SISU>; " + Next);
        (await LinksDaListagemDeTeste.ComPaginaAnteriorAsync("?tipoProcesso=SISU")).Should().Contain("&tipoProcesso=SISU>; rel=\"prev\"");
    }

    [Fact(DisplayName = "Filtro simples repetido leva só o primeiro valor; filtro múltiplo leva os valores sem repetição")]
    public async Task Link_FiltroSimplesEMultiplo_RepeteConformeADeclaracao()
    {
        string link = await LinksDaListagemDeTeste.ComProximaPaginaAsync("?tipoProcesso=SISU&tipoProcesso=PSIQ&tipo=3&tipo=4&tipo=3");

        link.Should().Contain("&tipo=3&tipo=4&tipoProcesso=SISU>; " + Next);
    }

    [Fact(DisplayName = "Filtro de tipo composto é repetido pelos nomes das propriedades")]
    public async Task Link_FiltroDeTipoComposto_RepetePelosNomesDasPropriedades()
    {
        string link = await LinksDaListagemDeTeste.ComProximaPaginaAsync(
            "?cadastro=a&ato=1&ato=2&ato=1&outro=x", nameof(CursorDeTesteController.ListarComFiltroComposto));

        link.Should().Contain("&Cadastro=a&Ato=1&Ato=2>; " + Next).And.NotContain("outro");
    }

    [Fact(DisplayName = "Os parâmetros da própria paginação nunca contam como filtro")]
    public async Task Link_LimitEDirectionComoFiltro_NaoSaoDuplicados()
    {
        string link = await LinksDaListagemDeTeste.ComProximaPaginaAsync("?limit=10&direction=next&AfterId=x&q=a");

        link.Should().NotContain("AfterId").And.NotContain("limit=10&direction=next&limit");
    }

    [Theory(DisplayName = "O valor no limite é repetido e o acima do limite fica de fora")]
    [InlineData(256, true)]
    [InlineData(257, false)]
    public async Task Link_ValorNoLimiteEAcimaDele_SoORepeteQuandoCabe(int tamanho, bool repete)
    {
        string valor = new('v', tamanho);

        string link = await LinksDaListagemDeTeste.ComProximaPaginaAsync($"?tipoProcesso={valor}");

        link.Contains(valor, StringComparison.Ordinal).Should().Be(repete);
    }

    [Fact(DisplayName = "O limite de comprimento não conta os espaços das pontas do texto pesquisado")]
    public async Task Link_BuscaComEspacosNasPontas_MedeOTextoAparado()
    {
        string termo = new('a', 256);

        string link = await LinksDaListagemDeTeste.ComProximaPaginaAsync($"?q={Uri.EscapeDataString("   " + termo + "   ")}");

        link.Should().Contain($"&q={termo}>; " + Next);
    }

    [Fact(DisplayName = "Cabeçalho acima do máximo cai para links só com a paginação, mantendo self, next e prev")]
    public async Task Link_CabecalhoAcimaDoMaximo_CaiParaSoAPaginacao()
    {
        CursorPaginationOptions opcoes = new() { ComprimentoMaximoDoCabecalhoLink = 400 };
        string consulta = "?" + string.Join('&', Enumerable.Range(0, 40).Select(static n => $"tipo={n}"));

        string link = await LinksDaListagemDeTeste.ComProximaPaginaAsync(consulta, opcoes: opcoes);

        link.Should().NotContain("tipo=").And.Contain("rel=\"self\"").And.Contain(Next);
    }
}
