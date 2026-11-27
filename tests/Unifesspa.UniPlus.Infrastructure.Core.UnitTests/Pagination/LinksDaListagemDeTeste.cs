namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Pagination;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Apoio.ControllersDeTeste;
using Unifesspa.UniPlus.Kernel.Pagination;

using static CursorBinderTestSupport;

/// <summary>
/// Responde uma página de teste pelo caminho real de <c>OkPaginatedAsync</c> e devolve o cabeçalho <c>Link</c>.
/// </summary>
internal static class LinksDaListagemDeTeste
{
    internal const string EnderecoDaListagem = "https://uniplus.test/api/cursos";

    internal static Task<string> ComProximaPaginaAsync(string consulta, string acao = nameof(CursorDeTesteController.ListarComFiltros), CursorPaginationOptions? opcoes = null) =>
        CabecalhoAsync(consulta, acao, anterior: null, PontoDeContinuacao(Guid.NewGuid()), opcoes);

    internal static Task<string> SemProximaPaginaAsync(string consulta, string acao = nameof(CursorDeTesteController.ListarComFiltros)) =>
        CabecalhoAsync(consulta, acao, anterior: null, seguinte: null, opcoes: null);

    internal static Task<string> ComPaginaAnteriorAsync(string consulta, string acao = nameof(CursorDeTesteController.ListarComFiltros)) =>
        CabecalhoAsync(consulta, acao, PontoDeContinuacao(Guid.NewGuid()), seguinte: null, opcoes: null);

    private static async Task<string> CabecalhoAsync(
        string consulta, string acao, (string SortKey, Guid Id)? anterior, (string SortKey, Guid Id)? seguinte, CursorPaginationOptions? opcoes)
    {
        ServiceCollection servicos = CreateCursorServices();
        if (opcoes is not null)
            servicos.AddSingleton(Options.Create(opcoes));

        DefaultHttpContext contexto = ContextoDaRequisicao(consulta, servicos);
        ListagemController controller = new() { ControllerContext = ControllerContextOfAction(contexto, acao) };

        await controller.OkPaginatedAsync(Array.Empty<string>(), anterior, seguinte, new PageRequest(null, 20, PaginationDirection.Next));

        return contexto.Response.Headers["Link"].ToString();
    }

    private static DefaultHttpContext ContextoDaRequisicao(string consulta, ServiceCollection servicos)
    {
        DefaultHttpContext contexto = new() { RequestServices = servicos.BuildServiceProvider() };
        contexto.Request.Scheme = "https";
        contexto.Request.Host = new HostString("uniplus.test");
        contexto.Request.Path = "/api/cursos";
        contexto.Request.QueryString = new QueryString(consulta);
        return contexto;
    }

    private sealed class ListagemController : ControllerBase;
}
