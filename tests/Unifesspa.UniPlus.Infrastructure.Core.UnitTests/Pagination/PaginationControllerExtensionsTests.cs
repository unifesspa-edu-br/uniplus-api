namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Pagination;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Infrastructure.Core.Cryptography;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Pagination;

public sealed class PaginationControllerExtensionsTests
{
    private const string EnderecoDaListagem = "https://uniplus.test/api/cursos";

    private sealed class ListagemController : ControllerBase;

    [Theory(DisplayName = "O link self devolve o q da requisição como veio, inclusive em branco")]
    [InlineData("?q=%20%20%20", "?q=%20%20%20")]
    [InlineData("?q=a%25b_c%5Cd", "?q=a%25b_c%5Cd")]
    [InlineData("", "")]
    public async Task OkPaginatedAsync_Self_EcoaOQOriginalDaRequisicao(string consulta, string esperado)
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
    public async Task OkPaginatedAsync_Next_EcoaOQEmBrancoDaRequisicao()
    {
        string link = await LinkDaListagemAsync("?q=%20%20%20", proximaPagina: true);

        link.Should().Contain("&q=%20%20%20>; rel=\"next\"");
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

    private static async Task<string> LinkDaListagemAsync(string consulta, bool proximaPagina)
    {
        DefaultHttpContext contexto = ContextoDaRequisicao(consulta);
        ListagemController controller = new() { ControllerContext = new ControllerContext { HttpContext = contexto } };
        Guid? seguinte = proximaPagina ? Guid.NewGuid() : null;

        await controller.OkPaginatedAsync(
            Array.Empty<string>(), null, seguinte, new PageRequest(null, 20, PaginationDirection.Next), "cursos");

        return contexto.Response.Headers["Link"].ToString();
    }

    private static DefaultHttpContext ContextoDaRequisicao(string consulta)
    {
        DefaultHttpContext contexto = new() { RequestServices = ServicosDaPaginacao() };
        contexto.Request.Scheme = "https";
        contexto.Request.Host = new HostString("uniplus.test");
        contexto.Request.Path = "/api/cursos";
        contexto.Request.QueryString = new QueryString(consulta);
        return contexto;
    }

    private static ServiceProvider ServicosDaPaginacao()
    {
        IOptions<CursorPaginationOptions> opcoes = Options.Create(new CursorPaginationOptions());
        IOptions<EncryptionOptions> opcoesDaCifra = Options.Create(
            new EncryptionOptions { Provider = "local", LocalKey = Convert.ToBase64String(new byte[32]) });
        LocalAesEncryptionService cifra = new(opcoesDaCifra, NullLogger<LocalAesEncryptionService>.Instance);

        return new ServiceCollection()
            .AddSingleton(TimeProvider.System)
            .AddSingleton(opcoes)
            .AddSingleton(new CursorEncoder(cifra, opcoes, TimeProvider.System))
            .BuildServiceProvider();
    }
}
