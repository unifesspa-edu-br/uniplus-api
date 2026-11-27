namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Pagination;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

public sealed class PaginationControllerExtensionsTests
{
    private const string EnderecoDaListagem = LinksDaListagemDeTeste.EnderecoDaListagem;

    [Theory(DisplayName = "O link self devolve o q da requisição sem os espaços das pontas")]
    [InlineData("?q=%20%20%20", "?q=")]
    [InlineData("?q=%20%20a%20b%20%20", "?q=a%20b")]
    [InlineData("?q=%09a%0A", "?q=a")]
    [InlineData("?q=a%25b_c%5Cd", "?q=a%25b_c%5Cd")]
    [InlineData("", "")]
    public async Task OkPaginatedAsync_Self_EcoaOQSemOsEspacosDasPontas(string consulta, string esperado)
    {
        string link = await LinksDaListagemDeTeste.SemProximaPaginaAsync(consulta);

        link.Should().Contain($"<{EnderecoDaListagem}{esperado}>; rel=\"self\"");
    }

    [Fact]
    public async Task OkPaginatedAsync_RequisicaoSemQ_NaoEmiteQNosLinks()
    {
        string link = await LinksDaListagemDeTeste.ComProximaPaginaAsync(string.Empty);

        link.Should().NotContain("q=");
    }

    [Fact]
    public async Task OkPaginatedAsync_Next_EcoaOQEmBrancoSemEspacos()
    {
        string link = await LinksDaListagemDeTeste.ComProximaPaginaAsync("?q=%20%20%20");

        link.Should().Contain("&q=>; rel=\"next\"");
    }

    [Fact]
    public async Task OkPaginatedAsync_DuzentosCaracteresComMilharesDeEspacosNasPontas_LinksTrazemOTermoAparado()
    {
        string espacos = new(' ', 3000);
        string termo = new('a', TermoDeBusca.ComprimentoMaximo);
        string consulta = $"?q={Uri.EscapeDataString(espacos + termo + espacos)}";

        string link = await LinksDaListagemDeTeste.ComProximaPaginaAsync(consulta);

        link.Should().Contain($"&q={termo}>; rel=\"next\"");
        link.Should().Contain($"?q={termo}>; rel=\"self\"");
        link.Should().NotContain("%20");
    }

    [Fact]
    public async Task OkPaginatedAsync_Prev_EcoaOQSemOsEspacosDasPontas()
    {
        string link = await LinksDaListagemDeTeste.ComPaginaAnteriorAsync("?q=%20%20processo%20%20");

        link.Should().Contain("&q=processo>; rel=\"prev\"");
    }

    [Theory(DisplayName = "O parâmetro busca é aparado como o q")]
    [InlineData("?busca=%20%20decisao%20%20", "&busca=decisao>; rel=\"next\"")]
    [InlineData("?BUSCA=%20%20decisao%20%20", "&busca=decisao>; rel=\"next\"")]
    public async Task OkPaginatedAsync_Busca_EcoaOTextoSemOsEspacosDasPontas(string consulta, string esperado)
    {
        string link = await LinksDaListagemDeTeste.ComProximaPaginaAsync(consulta);

        link.Should().Contain(esperado);
    }

    [Theory(DisplayName = "Filtro que não é busca volta como veio, inclusive em branco")]
    [InlineData("?tipoProcesso=%20", "?tipoProcesso=%20>; rel=\"self\"")]
    [InlineData("?tipoProcesso=%20", "&tipoProcesso=%20>; rel=\"next\"")]
    [InlineData("?tipoProcesso=%20x%20&q=%20y%20", "?q=y&tipoProcesso=%20x%20>; rel=\"self\"")]
    public async Task OkPaginatedAsync_FiltroQueNaoEBusca_PreservaOValorBruto(string consulta, string esperado)
    {
        string link = await LinksDaListagemDeTeste.ComProximaPaginaAsync(consulta);

        link.Should().Contain(esperado);
    }

    [Fact]
    public async Task OkPaginatedAsync_EspacosNoMeioDoValor_Preserva()
    {
        string link = await LinksDaListagemDeTeste.ComProximaPaginaAsync("?q=%20processo%20seletivo%20");

        link.Should().Contain("&q=processo%20seletivo>; rel=\"next\"");
    }

    [Fact]
    public async Task OkPaginatedAsync_Next_EcoaOQComCuringasIntegral()
    {
        string link = await LinksDaListagemDeTeste.ComProximaPaginaAsync("?q=a%25b_c%5Cd");

        link.Should().Contain("&q=a%25b_c%5Cd>; rel=\"next\"");
    }

    [Fact]
    public async Task OkPaginatedAsync_OutrosFiltros_SePreservamNosLinks()
    {
        string link = await LinksDaListagemDeTeste.ComProximaPaginaAsync("?q=a%25b&tipo=3&tipo=4");

        link.Should().Contain("&q=a%25b&tipo=3&tipo=4>; rel=\"next\"");
        link.Should().Contain("?q=a%25b&tipo=3&tipo=4>; rel=\"self\"");
    }
}
