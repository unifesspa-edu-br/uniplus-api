namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Pagination;

using System.Reflection;

using AwesomeAssertions;

using NSubstitute;

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

    // ─── User-binding (story #312, ADR-0026) ─────────────────────────────────

    [Fact]
    public async Task BindModelAsync_RequireUserBinding_CursorComUserIdQueBate_RetornaSuccess()
    {
        Guid afterId = Guid.CreateVersion7();
        ServiceProvider services = BuildServicesWithUser("user-alice");
        CursorEncoder encoder = services.GetRequiredService<CursorEncoder>();
        string cursor = await encoder.EncodeAsync(new CursorPayload(
            After: afterId.ToString(),
            Limit: 10,
            ResourceTag: "inscricoes",
            ExpiresAt: DateTimeOffset.UtcNow.AddMinutes(10),
            UserId: "user-alice"));

        DefaultModelBindingContext context = CreateContext(
            "inscricoes", $"?cursor={Uri.EscapeDataString(cursor)}",
            servicesOverride: services, methodName: nameof(TestActions.UserScopedAction));

        await new PageRequestModelBinder().BindModelAsync(context);

        context.Result.IsModelSet.Should().BeTrue();
        ((PageRequest)context.Result.Model!).AfterId.Should().Be(afterId);
    }

    [Fact]
    public async Task BindModelAsync_RequireUserBinding_CursorComUserIdDeOutroUser_RetornaInvalido()
    {
        Guid afterId = Guid.CreateVersion7();
        ServiceProvider services = BuildServicesWithUser("user-bob");
        CursorEncoder encoder = services.GetRequiredService<CursorEncoder>();
        // Cursor emitido para Alice — Bob tenta usar.
        string cursor = await encoder.EncodeAsync(new CursorPayload(
            After: afterId.ToString(),
            Limit: 10,
            ResourceTag: "inscricoes",
            ExpiresAt: DateTimeOffset.UtcNow.AddMinutes(10),
            UserId: "user-alice"));

        DefaultModelBindingContext context = CreateContext(
            "inscricoes", $"?cursor={Uri.EscapeDataString(cursor)}",
            servicesOverride: services, methodName: nameof(TestActions.UserScopedAction));

        await new PageRequestModelBinder().BindModelAsync(context);

        context.Result.IsModelSet.Should().BeFalse();
        // Mismatch retorna Invalido (não Forbidden) — não vaza status de
        // "cursor existe mas é de outro user".
        context.HttpContext.Items[CursorBindingErrorCodes.HttpContextItemKey]
            .Should().Be(CursorBindingErrorCodes.Invalido);
    }

    [Fact]
    public async Task BindModelAsync_RequireUserBinding_CursorSemUserIdLegacy_RetornaInvalido()
    {
        Guid afterId = Guid.CreateVersion7();
        ServiceProvider services = BuildServicesWithUser("user-alice");
        CursorEncoder encoder = services.GetRequiredService<CursorEncoder>();
        // Cursor sem UserId (era público quando emitido) — endpoint user-scoped
        // não aceita.
        string cursor = await encoder.EncodeAsync(new CursorPayload(
            After: afterId.ToString(),
            Limit: 10,
            ResourceTag: "inscricoes",
            ExpiresAt: DateTimeOffset.UtcNow.AddMinutes(10),
            UserId: null));

        DefaultModelBindingContext context = CreateContext(
            "inscricoes", $"?cursor={Uri.EscapeDataString(cursor)}",
            servicesOverride: services, methodName: nameof(TestActions.UserScopedAction));

        await new PageRequestModelBinder().BindModelAsync(context);

        context.Result.IsModelSet.Should().BeFalse();
        context.HttpContext.Items[CursorBindingErrorCodes.HttpContextItemKey]
            .Should().Be(CursorBindingErrorCodes.Invalido);
    }

    [Fact]
    public async Task BindModelAsync_RequireUserBinding_Anonymous_RetornaInvalido()
    {
        Guid afterId = Guid.CreateVersion7();
        ServiceProvider services = BuildServicesWithUser(userId: null);
        CursorEncoder encoder = services.GetRequiredService<CursorEncoder>();
        string cursor = await encoder.EncodeAsync(new CursorPayload(
            After: afterId.ToString(),
            Limit: 10,
            ResourceTag: "inscricoes",
            ExpiresAt: DateTimeOffset.UtcNow.AddMinutes(10),
            UserId: "user-alice"));

        DefaultModelBindingContext context = CreateContext(
            "inscricoes", $"?cursor={Uri.EscapeDataString(cursor)}",
            servicesOverride: services, methodName: nameof(TestActions.UserScopedAction));

        await new PageRequestModelBinder().BindModelAsync(context);

        context.Result.IsModelSet.Should().BeFalse();
        context.HttpContext.Items[CursorBindingErrorCodes.HttpContextItemKey]
            .Should().Be(CursorBindingErrorCodes.Invalido);
    }

    [Fact]
    public async Task BindModelAsync_RequireUserBindingFalse_CursorComUserId_IgnoraBinding()
    {
        // Endpoint público (RequireUserBinding=false) não valida UserId mesmo
        // se vier preenchido. Mantém compat com cursores antigos e legacy.
        Guid afterId = Guid.CreateVersion7();
        ServiceProvider services = BuildServicesWithUser("user-anyone");
        CursorEncoder encoder = services.GetRequiredService<CursorEncoder>();
        string cursor = await encoder.EncodeAsync(new CursorPayload(
            After: afterId.ToString(),
            Limit: 10,
            ResourceTag: "editais",
            ExpiresAt: DateTimeOffset.UtcNow.AddMinutes(10),
            UserId: "user-someoneelse"));

        DefaultModelBindingContext context = CreateContext(
            "editais", $"?cursor={Uri.EscapeDataString(cursor)}",
            servicesOverride: services, methodName: nameof(TestActions.PaginatedAction));

        await new PageRequestModelBinder().BindModelAsync(context);

        context.Result.IsModelSet.Should().BeTrue();
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

    private static ServiceProvider BuildServicesWithUser(string? userId)
    {
        ServiceCollection services = new();
        services.AddSingleton<IUniPlusEncryptionService>(_ => new LocalAesEncryptionService(
            Options.Create(new EncryptionOptions { Provider = "local", LocalKey = Convert.ToBase64String(Key) }),
            NullLogger<LocalAesEncryptionService>.Instance));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<CursorEncoder>();
        services.AddSingleton(Options.Create(new CursorPaginationOptions()));

        Unifesspa.UniPlus.Application.Abstractions.Authentication.IUserContext userCtx =
            NSubstitute.Substitute.For<Unifesspa.UniPlus.Application.Abstractions.Authentication.IUserContext>();
        userCtx.IsAuthenticated.Returns(!string.IsNullOrEmpty(userId));
        userCtx.UserId.Returns(userId);
        services.AddSingleton(userCtx);

        return services.BuildServiceProvider();
    }

    private static DefaultModelBindingContext CreateContext(
        string resource,
        string queryString,
        ServiceProvider? servicesOverride = null,
        string methodName = nameof(TestActions.PaginatedAction))
    {
        ServiceProvider services = servicesOverride ?? BuildServices();

        // Atributo [FromCursor(...)] vem de uma assinatura real construída
        // por reflection — caminho que o binder também usa em produção, garantindo
        // que o teste exercita exatamente o ResolveAttribute.
        ParameterInfo parameterInfo = typeof(TestActions)
            .GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic)!
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
            ActionName = methodName,
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

        internal static void UserScopedAction(
            [FromCursor("inscricoes", RequireUserBinding = true)] PageRequest page)
        {
            _ = page;
        }
    }
}
