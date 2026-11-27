namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Pagination;

using AwesomeAssertions;

using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Apoio.ControllersDeTeste;
using Unifesspa.UniPlus.Kernel.Pagination;

using static CursorBinderTestSupport;

public sealed class PageRequestModelBinderVinculoDeUsuarioTests
{
    [Theory]
    [InlineData(false, null, "alice")]
    [InlineData(true, null, "alice")]
    [InlineData(true, "", "alice")]
    [InlineData(false, "alice", "alice")]
    [InlineData(true, "alice", null)]
    [InlineData(true, "alice", "")]
    [InlineData(true, "bob", "alice")]
    [InlineData(true, "Alice", "alice")]
    public async Task BindModelAsync_VinculoDeUsuarioQueNaoSeSustenta_RecusaSemDizerQualFalhou(
        bool authenticated, string? currentUser, string? cursorUser)
    {
        ResolutionCounter counter = new();
        ServiceProvider services = CreateCursorServices().WithUser(authenticated, currentUser, counter).BuildServiceProvider();
        string cursor = await EncodeAsync(services, ValidPayload("inscricoes") with { ResumoDaConta = ResumoDaContaDe(cursorUser) });

        ModelBindingContext context = await BindAsync(
            QueryWithCursor(cursor), nameof(CursorDeTesteController.UserInscricoes), services);

        AssertInvalidCursor(context);
    }

    [Fact]
    public async Task BindModelAsync_UsuarioDoCursorIgualAoAutenticado_AceitaOCursor()
    {
        ResolutionCounter counter = new();
        ServiceProvider services = CreateCursorServices().WithUser(true, "alice", counter).BuildServiceProvider();
        CursorPayload payload = ValidPayload("inscricoes") with { ResumoDaConta = ResumoDaContaDe("alice") };
        string cursor = await EncodeAsync(services, payload);

        ModelBindingContext context = await BindAsync(
            QueryWithCursor(cursor), nameof(CursorDeTesteController.UserInscricoes), services);

        AssertPage(context).AfterId.Should().Be(Guid.Parse(payload.After));
    }

    [Fact]
    public async Task BindModelAsync_EndpointSemVinculoDeUsuario_NaoResolveOUsuario()
    {
        ResolutionCounter counter = new();
        ServiceProvider services = CreateCursorServices().WithUser(false, null, counter).BuildServiceProvider();
        string cursor = await EncodeAsync(services, ValidPayload() with { ResumoDaConta = ResumoDaContaDe("alice") });

        ModelBindingContext context = await BindAsync(QueryWithCursor(cursor), services: services);

        AssertPage(context);
        counter.Total.Should().Be(0);
    }

    [Fact]
    public async Task BindModelAsync_EndpointPublicoComOutroUsuario_AceitaOCursor()
    {
        ResolutionCounter counter = new();
        ServiceProvider services = CreateCursorServices().WithUser(true, "user-anyone", counter).BuildServiceProvider();
        string cursor = await EncodeAsync(services, ValidPayload() with { ResumoDaConta = ResumoDaContaDe("user-someoneelse") });

        ModelBindingContext context = await BindAsync(QueryWithCursor(cursor), services: services);

        AssertPage(context);
    }

    [Fact]
    public async Task BindModelAsync_CursorInvalidoEmEndpointComVinculo_NaoResolveOUsuario()
    {
        ResolutionCounter counter = new();
        ServiceProvider services = CreateCursorServices().WithUser(true, "alice", counter).BuildServiceProvider();

        await BindAsync("?cursor=@@@@", nameof(CursorDeTesteController.UserInscricoes), services);

        counter.Total.Should().Be(0);
    }

    [Fact]
    public async Task BindModelAsync_CursorExpiradoEmEndpointComVinculo_NaoResolveOUsuario()
    {
        CursorPayload payload = ValidPayload("inscricoes") with
        {
            ExpiresAt = InstanteDaConsulta.AddMinutes(-1),
            ResumoDaConta = ResumoDaContaDe("alice"),
        };

        int resolutions = await CountUserResolutionsAsync(payload, string.Empty);

        resolutions.Should().Be(0);
    }

    [Fact]
    public async Task BindModelAsync_EtiquetaDeOutroRecursoEmEndpointComVinculo_NaoResolveOUsuario()
    {
        CursorPayload payload = ValidPayload("editais") with { ResumoDaConta = ResumoDaContaDe("alice") };

        int resolutions = await CountUserResolutionsAsync(payload, string.Empty);

        resolutions.Should().Be(0);
    }

    [Fact]
    public async Task BindModelAsync_DirecaoTrocadaEmEndpointComVinculo_NaoResolveOUsuario()
    {
        CursorPayload payload = ValidPayload("inscricoes") with { ResumoDaConta = ResumoDaContaDe("alice") };

        int resolutions = await CountUserResolutionsAsync(payload, "&direction=prev");

        resolutions.Should().Be(0);
    }

    [Fact]
    public async Task BindModelAsync_AfterQueNaoEGuidEmEndpointComVinculo_NaoResolveOUsuario()
    {
        CursorPayload payload = ValidPayload("inscricoes") with { After = "nao-e-guid", ResumoDaConta = ResumoDaContaDe("alice") };

        int resolutions = await CountUserResolutionsAsync(payload, string.Empty);

        resolutions.Should().Be(0);
    }

    [Fact]
    public async Task BindModelAsync_MesmaContaEmOutroEmissor_RecusaSemDizerQualFalhou()
    {
        ResolutionCounter counter = new();
        ServiceProvider services = CreateCursorServices().WithUser(true, "alice", counter, "https://outro-emissor.exemplo").BuildServiceProvider();
        string cursor = await EncodeAsync(services, ValidPayload("inscricoes") with { ResumoDaConta = ResumoDaContaDe("alice") });

        ModelBindingContext context = await BindAsync(
            QueryWithCursor(cursor), nameof(CursorDeTesteController.UserInscricoes), services);

        AssertInvalidCursor(context);
    }

    [Fact]
    public async Task BindModelAsync_VinculoDeUsuarioEChaveDeContinuacaoAusente_ResolveOUsuarioAntesDeRecusar()
    {
        CursorPayload payload = ValidPayload("inscricoes") with { ResumoDaConta = ResumoDaContaDe("alice"), SortKey = null };

        ResolutionCounter counter = new();
        ServiceProvider services = CreateCursorServices().WithUser(true, "alice", counter).BuildServiceProvider();
        string cursor = await EncodeAsync(services, payload);

        ModelBindingContext context = await BindAsync(
            QueryWithCursor(cursor), nameof(CursorDeTesteController.UserInscricoes), services);

        AssertInvalidCursor(context);
        counter.Total.Should().Be(1);
    }

    private static async Task<int> CountUserResolutionsAsync(CursorPayload payload, string querySuffix)
    {
        ResolutionCounter counter = new();
        ServiceProvider services = CreateCursorServices().WithUser(true, "alice", counter).BuildServiceProvider();
        string cursor = await EncodeAsync(services, payload);

        await BindAsync(
            QueryWithCursor(cursor, querySuffix), nameof(CursorDeTesteController.UserInscricoes), services);

        return counter.Total;
    }
}
