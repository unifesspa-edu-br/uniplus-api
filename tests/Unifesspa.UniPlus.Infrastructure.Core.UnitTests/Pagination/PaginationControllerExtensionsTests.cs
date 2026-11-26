namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Pagination;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Apoio.ControllersDeTeste;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Pagination;

using static CursorBinderTestSupport;

public sealed class PaginationControllerExtensionsTests
{
    private const string EnderecoDaListagem = "https://uniplus.test/api/cursos";

    private sealed class ListagemController : ControllerBase;

    [Theory(DisplayName = "O link self devolve o q da requisição sem os espaços das pontas")]
    [InlineData("?q=%20%20%20", "?q=")]
    [InlineData("?q=%20%20a%20b%20%20", "?q=a%20b")]
    [InlineData("?q=%09a%0A", "?q=a")]
    [InlineData("?q=a%25b_c%5Cd", "?q=a%25b_c%5Cd")]
    [InlineData("", "")]
    public async Task OkPaginatedAsync_Self_EcoaOQSemOsEspacosDasPontas(string consulta, string esperado)
    {
        string link = await LinkDaListagemAsync(consulta, proximaPagina: false);

        link.Should().Contain($"<{EnderecoDaListagem}{esperado}>; rel=\"self\"");
    }

    [Fact]
    public async Task OkPaginatedAsync_RequisicaoSemQ_NaoEmiteQNosLinks()
    {
        string link = await LinkDaListagemAsync(string.Empty, proximaPagina: true);

        link.Should().NotContain("q=");
    }

    [Fact]
    public async Task OkPaginatedAsync_Next_EcoaOQEmBrancoSemEspacos()
    {
        string link = await LinkDaListagemAsync("?q=%20%20%20", proximaPagina: true);

        link.Should().Contain("&q=>; rel=\"next\"");
    }

    [Fact]
    public async Task OkPaginatedAsync_DuzentosCaracteresComMilharesDeEspacosNasPontas_LinksTrazemOTermoAparado()
    {
        string espacos = new(' ', 3000);
        string termo = new('a', TermoDeBusca.ComprimentoMaximo);
        string consulta = $"?q={Uri.EscapeDataString(espacos + termo + espacos)}";

        string link = await LinkDaListagemAsync(consulta, proximaPagina: true);

        link.Should().Contain($"&q={termo}>; rel=\"next\"");
        link.Should().Contain($"?q={termo}>; rel=\"self\"");
        link.Should().NotContain("%20");
    }

    [Fact]
    public async Task OkPaginatedAsync_Prev_EcoaOQSemOsEspacosDasPontas()
    {
        string link = await LinkComPaginaAnteriorAsync("?q=%20%20processo%20%20");

        link.Should().Contain("&q=processo>; rel=\"prev\"");
    }

    [Theory(DisplayName = "O parâmetro busca é aparado como o q")]
    [InlineData("?busca=%20%20decisao%20%20", "&busca=decisao>; rel=\"next\"")]
    [InlineData("?BUSCA=%20%20decisao%20%20", "&BUSCA=decisao>; rel=\"next\"")]
    public async Task OkPaginatedAsync_Busca_EcoaOTextoSemOsEspacosDasPontas(string consulta, string esperado)
    {
        string link = await LinkDaListagemAsync(consulta, proximaPagina: true);

        link.Should().Contain(esperado);
    }

    [Theory(DisplayName = "Filtro que não é busca volta como veio, inclusive em branco")]
    [InlineData("?tipoProcesso=%20", "?tipoProcesso=%20>; rel=\"self\"")]
    [InlineData("?tipoProcesso=%20", "&tipoProcesso=%20>; rel=\"next\"")]
    [InlineData("?tipoProcesso=%20x%20&q=%20y%20", "?tipoProcesso=%20x%20&q=y>; rel=\"self\"")]
    public async Task OkPaginatedAsync_FiltroQueNaoEBusca_PreservaOValorBruto(string consulta, string esperado)
    {
        string link = await LinkDaListagemAsync(consulta, proximaPagina: true);

        link.Should().Contain(esperado);
    }

    [Fact]
    public async Task OkPaginatedAsync_EspacosNoMeioDoValor_Preserva()
    {
        string link = await LinkDaListagemAsync("?q=%20processo%20seletivo%20", proximaPagina: true);

        link.Should().Contain("&q=processo%20seletivo>; rel=\"next\"");
    }

    [Fact]
    public async Task OkPaginatedAsync_Next_EcoaOQComCuringasIntegral()
    {
        string link = await LinkDaListagemAsync("?q=a%25b_c%5Cd", proximaPagina: true);

        link.Should().Contain("&q=a%25b_c%5Cd>; rel=\"next\"");
    }

    [Fact]
    public async Task OkPaginatedAsync_OutrosFiltros_SePreservamNosLinks()
    {
        string link = await LinkDaListagemAsync("?q=a%25b&tipo=3&tipo=4", proximaPagina: true);

        link.Should().Contain("&q=a%25b&tipo=3&tipo=4>; rel=\"next\"");
        link.Should().Contain("?q=a%25b&tipo=3&tipo=4>; rel=\"self\"");
    }

    private static Task<string> LinkDaListagemAsync(string consulta, bool proximaPagina) =>
        LinkDaListagemAsync(consulta, anterior: null, proximaPagina ? Guid.NewGuid() : null);

    private static Task<string> LinkComPaginaAnteriorAsync(string consulta) =>
        LinkDaListagemAsync(consulta, Guid.NewGuid(), seguinte: null);

    private static async Task<string> LinkDaListagemAsync(string consulta, Guid? anterior, Guid? seguinte)
    {
        DefaultHttpContext contexto = ContextoDaRequisicao(consulta);
        ListagemController controller = new()
        {
            ControllerContext = ControllerContextOfAction(contexto, nameof(CursorDeTesteController.Listar)),
        };

        await controller.OkPaginatedAsync(
            Array.Empty<string>(), anterior, seguinte, new PageRequest(null, 20, PaginationDirection.Next));

        return contexto.Response.Headers["Link"].ToString();
    }

    private static DefaultHttpContext ContextoDaRequisicao(string consulta)
    {
        DefaultHttpContext contexto = new() { RequestServices = CreateCursorServices().BuildServiceProvider() };
        contexto.Request.Scheme = "https";
        contexto.Request.Host = new HostString("uniplus.test");
        contexto.Request.Path = "/api/cursos";
        contexto.Request.QueryString = new QueryString(consulta);
        return contexto;
    }
}
