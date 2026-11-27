namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Pagination;

using System.Web;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Apoio.ControllersDeTeste;
using Unifesspa.UniPlus.Kernel.Pagination;

using static CursorBinderTestSupport;

public sealed class PageResponseWriterTests
{
    private const string AcaoDeUsuario = nameof(CursorDeTesteController.UserInscricoes);
    private const string AcaoEscopada = nameof(CursorDeTesteController.ScopedByEntity);

    private static readonly PageRequest PaginaDeVinteItens = new(null, 20, PaginationDirection.Next);

    private sealed class ListagemController : ControllerBase;

    [Fact]
    public async Task OkPaginatedAsync_AcaoDeUsuario_CursorEmitidoEAceitoPeloBinderParaOMesmoUsuario()
    {
        ServiceProvider services = CreateCursorServices().WithUser(true, "alice", new ResolutionCounter()).BuildServiceProvider();
        ListagemController controller = ControllerDaAcao(services, AcaoDeUsuario);
        Guid proximo = Guid.NewGuid();

        await controller.OkPaginatedAsync(Array.Empty<string>(), null, PontoDeContinuacao(proximo), PaginaDeVinteItens);

        ModelBindingContext context = await BindAsync(QueryWithCursor(CursorDaProximaPagina(controller)), AcaoDeUsuario, services);
        PageRequest pagina = AssertPage(context);
        pagina.AfterId.Should().Be(proximo);
        pagina.AfterSortKey.Should().Be(ChaveDeTeste);
    }

    [Fact]
    public async Task OkPaginatedAsync_AcaoDeUsuarioSemUsuarioAutenticado_Lanca()
    {
        ServiceProvider services = CreateCursorServices().WithUser(false, null, new ResolutionCounter()).BuildServiceProvider();
        ListagemController controller = ControllerDaAcao(services, AcaoDeUsuario);

        Func<Task> emitir = () => controller.OkPaginatedAsync(Array.Empty<string>(), null, PontoDeContinuacao(Guid.NewGuid()), PaginaDeVinteItens);

        await emitir.Should().ThrowAsync<InvalidOperationException>().WithMessage("*RequireUserBinding = true*");
    }

    [Fact]
    public async Task OkPaginatedAsync_AcaoDeUsuarioSemCursorParaEmitir_NaoResolveOUsuario()
    {
        ResolutionCounter counter = new();
        ServiceProvider services = CreateCursorServices().WithUser(false, null, counter).BuildServiceProvider();
        ListagemController controller = ControllerDaAcao(services, AcaoDeUsuario);

        await controller.OkPaginatedAsync(Array.Empty<string>(), null, null, PaginaDeVinteItens);

        counter.Total.Should().Be(0);
    }

    [Fact]
    public async Task OkPaginatedAsync_AcaoEscopadaPelaRota_CursorVoltaPeloBinderNaMesmaEntidade()
    {
        ServiceProvider services = CreateCursorServices().BuildServiceProvider();
        Dictionary<string, object?> rota = RotaDaEntidade(Guid.NewGuid());
        Guid proximo = Guid.NewGuid();

        string cursor = await CursorEmitidoNaRotaAsync(services, rota, proximo);

        AssertPage(await BindNaRotaAsync(services, cursor, rota)).AfterId.Should().Be(proximo);
    }

    [Fact]
    public async Task OkPaginatedAsync_AcaoEscopadaPelaRota_CursorNaoVoltaPeloBinderEmOutraEntidade()
    {
        ServiceProvider services = CreateCursorServices().BuildServiceProvider();

        string cursor = await CursorEmitidoNaRotaAsync(services, RotaDaEntidade(Guid.NewGuid()), Guid.NewGuid());

        AssertInvalidCursor(await BindNaRotaAsync(services, cursor, RotaDaEntidade(Guid.NewGuid())));
    }

    [Fact]
    public async Task OkPaginatedAsync_ControllerSemFromCursorNaAcao_Lanca()
    {
        ServiceProvider services = CreateCursorServices().BuildServiceProvider();
        ListagemController controller = new() { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { RequestServices = services } } };

        Func<Task> emitir = () => controller.OkPaginatedAsync(Array.Empty<string>(), null, PontoDeContinuacao(Guid.NewGuid()), PaginaDeVinteItens);

        await emitir.Should().ThrowAsync<InvalidOperationException>().WithMessage("*[FromCursor*");
    }

    private static Dictionary<string, object?> RotaDaEntidade(Guid entidade) =>
        new() { ["entidadeTipo"] = "processo", ["entidadeId"] = entidade.ToString() };

    private static async Task<string> CursorEmitidoNaRotaAsync(IServiceProvider services, Dictionary<string, object?> rota, Guid proximo)
    {
        ListagemController controller = ControllerDaAcao(services, AcaoEscopada, rota);

        await controller.OkPaginatedAsync(Array.Empty<string>(), null, PontoDeContinuacao(proximo), PaginaDeVinteItens);

        return CursorDaProximaPagina(controller);
    }

    private static async Task<ModelBindingContext> BindNaRotaAsync(IServiceProvider services, string cursor, Dictionary<string, object?> rota)
    {
        DefaultModelBindingContext context = CreateContext(services, QueryWithCursor(cursor), AcaoEscopada, rota);
        await new PageRequestModelBinder().BindModelAsync(context);
        return context;
    }

    private static ListagemController ControllerDaAcao(
        IServiceProvider services,
        string acao,
        IDictionary<string, object?>? rota = null)
    {
        DefaultHttpContext httpContext = new() { RequestServices = services };
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("uniplus.test");
        httpContext.Request.Path = "/teste/cursor/listar";
        return new ListagemController { ControllerContext = ControllerContextOfAction(httpContext, acao, rota) };
    }

    private static string CursorDaProximaPagina(ControllerBase controller)
    {
        string link = controller.Response.Headers["Link"].ToString();
        string seguinte = link.Split(", ").Single(parte => parte.EndsWith("rel=\"next\"", StringComparison.Ordinal));
        string endereco = seguinte[1..seguinte.IndexOf('>', StringComparison.Ordinal)];

        return HttpUtility.ParseQueryString(new Uri(endereco).Query)["cursor"]!;
    }
}
