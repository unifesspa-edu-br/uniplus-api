namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Pagination;

using System.Reflection;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Infrastructure.Core.Cryptography;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;

public sealed class PageRequestModelBinderTests
{
    private static readonly byte[] Key = new byte[32];

    [Fact]
    public async Task BindModelAsync_SemCursorSemLimit_RetornaPageRequestComDefaults()
    {
        ModelBindingContext context = CreateContext("editais", queryString: "");

        await new PageRequestModelBinder().BindModelAsync(context);

        context.Result.IsModelSet.Should().BeTrue();
        PageRequest page = context.Result.Model.Should().BeOfType<PageRequest>().Subject;
        page.AfterId.Should().BeNull();
        page.Limit.Should().Be(20); // CursorPaginationOptions.LimitDefault
    }

    [Fact]
    public async Task BindModelAsync_CursorWhitespaceOnly_TratadoComoCursorAusente()
    {
        ModelBindingContext context = CreateContext("editais", queryString: "?cursor=%20%20%20");

        await new PageRequestModelBinder().BindModelAsync(context);

        context.Result.IsModelSet.Should().BeTrue();
        PageRequest page = (PageRequest)context.Result.Model!;
        page.AfterId.Should().BeNull();
        page.Limit.Should().Be(20);
    }

    [Fact]
    public async Task BindModelAsync_LimitNaoNumerico_RetornaLimitInvalido()
    {
        ModelBindingContext context = CreateContext("editais", queryString: "?limit=abc");

        await new PageRequestModelBinder().BindModelAsync(context);

        context.Result.IsModelSet.Should().BeFalse();
        context.HttpContext.Items[CursorBindingErrorCodes.HttpContextItemKey]
            .Should().Be(CursorBindingErrorCodes.LimitInvalido);
    }

    [Fact]
    public async Task BindModelAsync_LimitNegativo_RetornaLimitInvalido()
    {
        ModelBindingContext context = CreateContext("editais", queryString: "?limit=-5");

        await new PageRequestModelBinder().BindModelAsync(context);

        context.Result.IsModelSet.Should().BeFalse();
        context.HttpContext.Items[CursorBindingErrorCodes.HttpContextItemKey]
            .Should().Be(CursorBindingErrorCodes.LimitInvalido);
    }

    [Fact]
    public async Task BindModelAsync_LimitZero_RetornaLimitInvalido()
    {
        ModelBindingContext context = CreateContext("editais", queryString: "?limit=0");

        await new PageRequestModelBinder().BindModelAsync(context);

        context.Result.IsModelSet.Should().BeFalse();
        context.HttpContext.Items[CursorBindingErrorCodes.HttpContextItemKey]
            .Should().Be(CursorBindingErrorCodes.LimitInvalido);
    }

    [Fact]
    public async Task BindModelAsync_LimitAcimaDoMaximo_RetornaLimitInvalido()
    {
        ModelBindingContext context = CreateContext("editais", queryString: "?limit=999");

        await new PageRequestModelBinder().BindModelAsync(context);

        context.Result.IsModelSet.Should().BeFalse();
        context.HttpContext.Items[CursorBindingErrorCodes.HttpContextItemKey]
            .Should().Be(CursorBindingErrorCodes.LimitInvalido);
    }

    [Fact]
    public async Task BindModelAsync_CursorBase64UrlInvalido_RetornaCursorInvalido()
    {
        ModelBindingContext context = CreateContext("editais", queryString: "?cursor=@@@@");

        await new PageRequestModelBinder().BindModelAsync(context);

        context.Result.IsModelSet.Should().BeFalse();
        context.HttpContext.Items[CursorBindingErrorCodes.HttpContextItemKey]
            .Should().Be(CursorBindingErrorCodes.Invalido);
    }

    [Fact]
    public async Task BindModelAsync_CursorComResourceTagDeOutroRecurso_RetornaCursorInvalido()
    {
        // Cliente envia cursor cifrado para "inscricoes" no endpoint de "editais".
        // Mesma chave de cifra (cifra OK), mas ResourceTag mismatch.
        ServiceProvider services = BuildServices();
        CursorEncoder encoder = services.GetRequiredService<CursorEncoder>();
        string foreignCursor = await encoder.EncodeAsync(new CursorPayload(
            After: Guid.NewGuid().ToString(),
            Limit: 10,
            ResourceTag: "inscricoes",
            ExpiresAt: DateTimeOffset.UtcNow.AddMinutes(10)));

        ModelBindingContext context = CreateContext(
            "editais",
            queryString: $"?cursor={Uri.EscapeDataString(foreignCursor)}",
            servicesOverride: services);

        await new PageRequestModelBinder().BindModelAsync(context);

        context.Result.IsModelSet.Should().BeFalse();
        context.HttpContext.Items[CursorBindingErrorCodes.HttpContextItemKey]
            .Should().Be(CursorBindingErrorCodes.Invalido);
    }

    [Fact]
    public async Task BindModelAsync_CursorAfterNaoEhGuidValido_RetornaCursorInvalido()
    {
        ServiceProvider services = BuildServices();
        CursorEncoder encoder = services.GetRequiredService<CursorEncoder>();
        string malformedAfter = await encoder.EncodeAsync(new CursorPayload(
            After: "not-a-guid",
            Limit: 10,
            ResourceTag: "editais",
            ExpiresAt: DateTimeOffset.UtcNow.AddMinutes(10)));

        ModelBindingContext context = CreateContext(
            "editais",
            queryString: $"?cursor={Uri.EscapeDataString(malformedAfter)}",
            servicesOverride: services);

        await new PageRequestModelBinder().BindModelAsync(context);

        context.Result.IsModelSet.Should().BeFalse();
        context.HttpContext.Items[CursorBindingErrorCodes.HttpContextItemKey]
            .Should().Be(CursorBindingErrorCodes.Invalido);
    }

    [Fact]
    public async Task BindModelAsync_CursorValido_RetornaPageRequestComAfterIdEDLimitDoCursor()
    {
        Guid expectedAfter = Guid.NewGuid();
        ServiceProvider services = BuildServices();
        CursorEncoder encoder = services.GetRequiredService<CursorEncoder>();
        string cursor = await encoder.EncodeAsync(new CursorPayload(
            After: expectedAfter.ToString(),
            Limit: 50,
            ResourceTag: "editais",
            ExpiresAt: DateTimeOffset.UtcNow.AddMinutes(10)));

        ModelBindingContext context = CreateContext(
            "editais",
            queryString: $"?cursor={Uri.EscapeDataString(cursor)}",
            servicesOverride: services);

        await new PageRequestModelBinder().BindModelAsync(context);

        context.Result.IsModelSet.Should().BeTrue();
        PageRequest page = (PageRequest)context.Result.Model!;
        page.AfterId.Should().Be(expectedAfter);
        page.Limit.Should().Be(50); // veio do cursor
    }

    [Fact]
    public async Task BindModelAsync_QueryStringLimitVenceCursorLimit()
    {
        Guid expectedAfter = Guid.NewGuid();
        ServiceProvider services = BuildServices();
        CursorEncoder encoder = services.GetRequiredService<CursorEncoder>();
        string cursor = await encoder.EncodeAsync(new CursorPayload(
            After: expectedAfter.ToString(),
            Limit: 10, // do cursor
            ResourceTag: "editais",
            ExpiresAt: DateTimeOffset.UtcNow.AddMinutes(10)));

        ModelBindingContext context = CreateContext(
            "editais",
            queryString: $"?cursor={Uri.EscapeDataString(cursor)}&limit=50",
            servicesOverride: services);

        await new PageRequestModelBinder().BindModelAsync(context);

        PageRequest page = (PageRequest)context.Result.Model!;
        page.Limit.Should().Be(50, "limit do query string vence sobre o do cursor");
    }

    [Fact]
    public async Task BindModelAsync_CursorExpirado_RetornaCursorExpirado()
    {
        Guid afterId = Guid.NewGuid();
        ServiceProvider services = BuildServices();
        CursorEncoder encoder = services.GetRequiredService<CursorEncoder>();
        string cursor = await encoder.EncodeAsync(new CursorPayload(
            After: afterId.ToString(),
            Limit: 10,
            ResourceTag: "editais",
            ExpiresAt: DateTimeOffset.UtcNow.AddMinutes(-1))); // já expirado

        ModelBindingContext context = CreateContext(
            "editais",
            queryString: $"?cursor={Uri.EscapeDataString(cursor)}",
            servicesOverride: services);

        await new PageRequestModelBinder().BindModelAsync(context);

        context.Result.IsModelSet.Should().BeFalse();
        context.HttpContext.Items[CursorBindingErrorCodes.HttpContextItemKey]
            .Should().Be(CursorBindingErrorCodes.Expirado);
    }

    // ─── Scaffolding ────────────────────────────────────────────────────────

    private static ServiceProvider BuildServices()
    {
        ServiceCollection services = new();
        services.AddSingleton<IUniPlusEncryptionService>(_ => new LocalAesEncryptionService(
            Options.Create(new EncryptionOptions { Provider = "local", LocalKey = Convert.ToBase64String(Key) }),
            NullLogger<LocalAesEncryptionService>.Instance));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<CursorEncoder>();
        services.AddSingleton(Options.Create(new CursorPaginationOptions()));
        return services.BuildServiceProvider();
    }

    private static DefaultModelBindingContext CreateContext(
        string resource,
        string queryString,
        ServiceProvider? servicesOverride = null)
    {
        ServiceProvider services = servicesOverride ?? BuildServices();

        // Atributo [FromCursor("editais")] vem de uma assinatura real construída
        // por reflection — caminho que o binder também usa em produção, garantindo
        // que o teste exercita exatamente o ResolveAttribute.
        ParameterInfo parameterInfo = typeof(TestActions)
            .GetMethod(nameof(TestActions.PaginatedAction), BindingFlags.Static | BindingFlags.NonPublic)!
            .GetParameters()[0];

        ControllerParameterDescriptor parameterDescriptor = new()
        {
            Name = parameterInfo.Name!,
            ParameterType = parameterInfo.ParameterType,
            ParameterInfo = parameterInfo,
        };

        ControllerActionDescriptor actionDescriptor = new()
        {
            Parameters = new List<ParameterDescriptor> { parameterDescriptor },
            ActionName = nameof(TestActions.PaginatedAction),
        };

        DefaultHttpContext httpContext = new() { RequestServices = services };
        httpContext.Request.QueryString = new QueryString(queryString);

        ActionContext actionContext = new(httpContext, new RouteData(), actionDescriptor);

        EmptyModelMetadataProvider metadataProvider = new();
        ModelMetadata metadata = metadataProvider.GetMetadataForType(typeof(PageRequest));

        return new DefaultModelBindingContext
        {
            ActionContext = actionContext,
            ModelMetadata = metadata,
            ModelName = parameterInfo.Name!,
            ModelState = new ModelStateDictionary(),
            ValueProvider = new CompositeValueProvider(),
        };
    }

    // Holder para reflection — métodos não-públicos válidos como fonte de
    // ParameterInfo com [FromCursor] aplicada.
    private static class TestActions
    {
        internal static void PaginatedAction([FromCursor("editais")] PageRequest page)
        {
            _ = page;
        }
    }
}
