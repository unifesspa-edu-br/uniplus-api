namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Pagination;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Infrastructure.Core.Cryptography;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Apoio;
using Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Apoio.ControllersDeTeste;
using Unifesspa.UniPlus.Kernel.Pagination;

internal static class CursorBinderTestSupport
{
    private static readonly AcoesDoMvcDeTeste Mvc = new(typeof(CursorDeTesteController));

    public static readonly DateTimeOffset InstanteDaConsulta = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public const string EmissorDeTeste = "https://emissor.exemplo/realms/uniplus";

    public const string InvalidCursorMessage = "O cursor informado é inválido.";

    public static ServiceCollection CreateCursorServices()
    {
        ServiceCollection services = new();
        services.AddSingleton<TimeProvider>(new FakeTimeProvider(InstanteDaConsulta));
        services.AddSingleton(Options.Create(new CursorPaginationOptions()));
        services.AddSingleton<IUniPlusEncryptionService>(_ => CreateCipher());
        services.AddSingleton<CursorEncoder>();
        services.AddSingleton(new FiltrosDasListagens(Mvc.Descricoes));
        return services;
    }

    public static CursorEncoder CreateEncoder() =>
        new(CreateCipher(), Options.Create(new CursorPaginationOptions()), new FakeTimeProvider(InstanteDaConsulta));

    public static ServiceCollection WithUser(
        this ServiceCollection services,
        bool authenticated,
        string? userId,
        ResolutionCounter counter,
        string issuer = EmissorDeTeste)
    {
        services.AddSingleton(_ =>
        {
            counter.Total++;
            IUserContext user = Substitute.For<IUserContext>();
            user.IsAuthenticated.Returns(authenticated);
            user.UserId.Returns(userId);
            user.Issuer.Returns(issuer);
            return user;
        });
        return services;
    }

    public static CursorPayload ValidPayload(string resource = "editais") => new(
        After: Guid.NewGuid().ToString(),
        Limit: 10,
        ResourceTag: resource,
        ExpiresAt: InstanteDaConsulta.AddMinutes(10),
        Direction: PaginationDirection.Next,
        SortKey: ChaveDeTeste);

    public const string ChaveDeTeste = "chave-de-continuacao";

    public static (string SortKey, Guid Id) PontoDeContinuacao(Guid id) => (ChaveDeTeste, id);

    public static string? ResumoDaContaDe(string? conta, string emissor = EmissorDeTeste) =>
        conta is null ? null : ResumoCanonico.De(emissor, conta);

    public static async Task<string> EncodeAsync(IServiceProvider services, CursorPayload payload)
    {
        CursorEncoder encoder = services.GetRequiredService<CursorEncoder>();
        return await encoder.EncodeAsync(payload);
    }

    public static string QueryWithCursor(string cursor, string suffix = "") =>
        $"?cursor={Uri.EscapeDataString(cursor)}{suffix}";

    public static async Task<DefaultModelBindingContext> BindAsync(
        string query,
        string? action = null,
        IServiceProvider? services = null)
    {
        DefaultModelBindingContext context = CreateContext(
            services ?? CreateCursorServices().BuildServiceProvider(), query, action);
        await new PageRequestModelBinder().BindModelAsync(context);
        return context;
    }

    public static DefaultModelBindingContext CreateContext(
        IServiceProvider services,
        string query,
        string? action = null,
        IDictionary<string, object?>? routeValues = null)
    {
        DefaultHttpContext httpContext = new() { RequestServices = services };
        httpContext.Request.QueryString = new QueryString(query);
        ControllerActionDescriptor descriptor = DescribeAction(action);
        RouteData route = new(new RouteValueDictionary(routeValues));

        return new DefaultModelBindingContext
        {
            ActionContext = new ActionContext(httpContext, route, descriptor),
            ModelMetadata = new EmptyModelMetadataProvider().GetMetadataForType(typeof(PageRequest)),
            ModelName = "pedido",
            ModelState = new ModelStateDictionary(),
            ValueProvider = new CompositeValueProvider(),
        };
    }

    public static void PreventQueryRead(ModelBindingContext context) =>
        context.HttpContext.Features.Set<IQueryFeature>(new UnreadableQueryFeature());

    public static void AssertRejection(ModelBindingContext context, string code, string message)
    {
        context.Result.IsModelSet.Should().BeFalse();
        context.HttpContext.Items[CursorBindingErrorCodes.HttpContextItemKey].Should().Be(code);
        context.ModelState[context.ModelName]!.Errors
            .Should().ContainSingle().Which.ErrorMessage.Should().Be(message);
    }

    public static void AssertInvalidCursor(ModelBindingContext context) =>
        AssertRejection(context, CursorBindingErrorCodes.Invalido, InvalidCursorMessage);

    public static PageRequest AssertPage(ModelBindingContext context)
    {
        context.Result.IsModelSet.Should().BeTrue();
        return context.Result.Model.Should().BeOfType<PageRequest>().Subject;
    }

    public static ControllerContext ControllerContextOfAction(
        HttpContext httpContext,
        string action,
        IDictionary<string, object?>? routeValues = null) =>
        new(new ActionContext(httpContext, new RouteData(new RouteValueDictionary(routeValues)), DescribeAction(action)));

    private static ControllerActionDescriptor DescribeAction(string? action = null) =>
        Mvc.Acao(typeof(CursorDeTesteController), action ?? nameof(CursorDeTesteController.PublicEditais));

    private static LocalAesEncryptionService CreateCipher() => new(
        Options.Create(new EncryptionOptions { Provider = "local", LocalKey = Convert.ToBase64String(new byte[32]) }),
        NullLogger<LocalAesEncryptionService>.Instance);
}

internal sealed class ResolutionCounter
{
    public int Total { get; set; }
}

internal sealed class UnreadableQueryFeature : IQueryFeature
{
    public IQueryCollection Query
    {
        get => throw new InvalidOperationException("A consulta não podia ser lida.");
        set => throw new InvalidOperationException("A consulta não podia ser lida.");
    }
}
