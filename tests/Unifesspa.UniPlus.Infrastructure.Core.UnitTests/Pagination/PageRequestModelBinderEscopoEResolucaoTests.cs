namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Pagination;

using AwesomeAssertions;

using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Apoio.ControllersDeTeste;

using static CursorBinderTestSupport;

public sealed class PageRequestModelBinderEscopoEResolucaoTests
{
    [Fact]
    public async Task BindModelAsync_ValoresDeRotaIguaisAosDaEmissao_AceitaOCursor()
    {
        Guid entity = Guid.NewGuid();

        DefaultModelBindingContext context = await BindScopedAsync(entity, entity.ToString());

        AssertPage(context);
    }

    [Fact]
    public async Task BindModelAsync_ValorDeRotaDeOutraEntidade_RecusaComoInvalido()
    {
        DefaultModelBindingContext context = await BindScopedAsync(Guid.NewGuid(), Guid.NewGuid().ToString());

        AssertInvalidCursor(context);
    }

    [Fact]
    public async Task BindModelAsync_ValorDeRotaEmCaixaAlta_AceitaOCursor()
    {
        Guid entity = Guid.NewGuid();

        DefaultModelBindingContext context = await BindScopedAsync(entity, entity.ToString().ToUpperInvariant());

        AssertPage(context);
    }

    [Fact]
    public async Task BindModelAsync_CursorComEtiquetaSemEscopoEmColecaoEscopada_RecusaComoInvalido()
    {
        ServiceProvider services = CreateCursorServices().BuildServiceProvider();
        string cursor = await EncodeAsync(services, ValidPayload("atos"));
        DefaultModelBindingContext context = CreateScopedContext(services, cursor, Guid.NewGuid().ToString());

        await new PageRequestModelBinder().BindModelAsync(context);

        AssertInvalidCursor(context);
    }

    [Fact]
    public async Task BindModelAsync_ValorDeRotaAusente_EsperaEtiquetaComSegmentoVazio()
    {
        ServiceProvider services = CreateCursorServices().BuildServiceProvider();
        string resourceTag = CursorResourceTag.Compose("atos", ["processo", null]);
        string cursor = await EncodeAsync(services, ValidPayload(resourceTag));
        DefaultModelBindingContext context = CreateScopedContext(services, cursor, entityId: null);

        await new PageRequestModelBinder().BindModelAsync(context);

        AssertPage(context);
    }

    [Fact]
    public async Task BindModelAsync_ParametroSemFromCursor_LancaSemResolverServicos()
    {
        DefaultModelBindingContext context = CreateContext(
            new ServiceCollection().BuildServiceProvider(), "?limit=0", nameof(CursorDeTesteController.WithoutAttribute));
        PreventQueryRead(context);

        Func<Task> bind = () => new PageRequestModelBinder().BindModelAsync(context);

        await bind.Should().ThrowAsync<InvalidOperationException>().WithMessage("*requer [FromCursor*");
    }

    [Fact]
    public async Task BindModelAsync_EncoderEOpcoesAusentes_LancaPeloEncoderSemLerAConsulta()
    {
        DefaultModelBindingContext context = CreateContext(
            new ServiceCollection().BuildServiceProvider(), "?limit=0", nameof(CursorDeTesteController.PublicEditais));
        PreventQueryRead(context);

        Func<Task> bind = () => new PageRequestModelBinder().BindModelAsync(context);

        await bind.Should().ThrowAsync<InvalidOperationException>().WithMessage("*CursorEncoder*");
    }

    [Fact]
    public async Task BindModelAsync_OpcoesAusentesComEncoderPresente_LancaPelasOpcoesSemLerAConsulta()
    {
        ServiceCollection services = new();
        services.AddSingleton(CreateEncoder());
        DefaultModelBindingContext context = CreateContext(
            services.BuildServiceProvider(), "?limit=0", nameof(CursorDeTesteController.PublicEditais));
        PreventQueryRead(context);

        Func<Task> bind = () => new PageRequestModelBinder().BindModelAsync(context);

        await bind.Should().ThrowAsync<InvalidOperationException>().WithMessage("*IOptions*CursorPaginationOptions*");
    }

    [Fact]
    public async Task BindModelAsync_EncoderEOpcoesLancando_SobeAExcecaoDoEncoder()
    {
        ServiceCollection services = new();
        services.AddSingleton<CursorEncoder>(_ => throw new InvalidOperationException("falha simulada do encoder"));
        services.AddSingleton<IOptions<CursorPaginationOptions>>(
            _ => throw new InvalidOperationException("falha simulada das opções"));
        DefaultModelBindingContext context = CreateContext(
            services.BuildServiceProvider(), "?limit=0", nameof(CursorDeTesteController.PublicEditais));
        PreventQueryRead(context);

        Func<Task> bind = () => new PageRequestModelBinder().BindModelAsync(context);

        await bind.Should().ThrowAsync<InvalidOperationException>().WithMessage("falha simulada do encoder");
    }

    [Fact]
    public async Task BindModelAsync_OpcoesLancandoComEncoderPresente_SobeAExcecaoDasOpcoes()
    {
        ServiceCollection services = new();
        services.AddSingleton(CreateEncoder());
        services.AddSingleton<IOptions<CursorPaginationOptions>>(
            _ => throw new InvalidOperationException("falha simulada das opções"));
        DefaultModelBindingContext context = CreateContext(
            services.BuildServiceProvider(), "?limit=0", nameof(CursorDeTesteController.PublicEditais));
        PreventQueryRead(context);

        Func<Task> bind = () => new PageRequestModelBinder().BindModelAsync(context);

        await bind.Should().ThrowAsync<InvalidOperationException>().WithMessage("falha simulada das opções");
    }

    [Fact]
    public async Task BindModelAsync_ServicosResolvidosComConsultaIlegivel_PropagaAFalhaDaConsulta()
    {
        DefaultModelBindingContext context = CreateContext(
            CreateCursorServices().BuildServiceProvider(), "?limit=0", nameof(CursorDeTesteController.PublicEditais));
        PreventQueryRead(context);

        Func<Task> bind = () => new PageRequestModelBinder().BindModelAsync(context);

        await bind.Should().ThrowAsync<InvalidOperationException>().WithMessage("A consulta não podia ser lida.");
    }

    private static async Task<DefaultModelBindingContext> BindScopedAsync(Guid issuedEntity, string routeEntity)
    {
        ServiceProvider services = CreateCursorServices().BuildServiceProvider();
        string resourceTag = CursorResourceTag.Compose("atos", ["processo", issuedEntity.ToString()]);
        string cursor = await EncodeAsync(services, ValidPayload(resourceTag));
        DefaultModelBindingContext context = CreateScopedContext(services, cursor, routeEntity);

        await new PageRequestModelBinder().BindModelAsync(context);
        return context;
    }

    private static DefaultModelBindingContext CreateScopedContext(
        IServiceProvider services,
        string cursor,
        string? entityId)
    {
        Dictionary<string, object?> route = new() { ["entidadeTipo"] = "processo" };
        if (entityId is not null)
        {
            route["entidadeId"] = entityId;
        }

        return CreateContext(services, QueryWithCursor(cursor), nameof(CursorDeTesteController.ScopedByEntity), route);
    }
}
