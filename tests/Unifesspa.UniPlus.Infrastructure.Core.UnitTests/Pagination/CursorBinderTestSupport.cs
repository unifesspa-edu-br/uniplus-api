namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Pagination;

using System.Reflection;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Infrastructure.Core.Cryptography;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Pagination;

internal static class CursorBinderTestSupport
{
    public const string InvalidCursorMessage = "O cursor informado é inválido.";

    public static ServiceCollection CreateCursorServices()
    {
        ServiceCollection services = new();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(Options.Create(new CursorPaginationOptions()));
        services.AddSingleton<IUniPlusEncryptionService>(_ => CreateCipher());
        services.AddSingleton<CursorEncoder>();
        return services;
    }

    public static CursorEncoder CreateEncoder() =>
        new(CreateCipher(), Options.Create(new CursorPaginationOptions()), TimeProvider.System);

    public static ServiceCollection WithUser(
        this ServiceCollection services,
        bool authenticated,
        string? userId,
        ResolutionCounter counter)
    {
        services.AddSingleton(_ =>
        {
            counter.Total++;
            IUserContext user = Substitute.For<IUserContext>();
            user.IsAuthenticated.Returns(authenticated);
            user.UserId.Returns(userId);
            return user;
        });
        return services;
    }

    public static CursorPayload ValidPayload(string resource = "editais") => new(
        After: Guid.CreateVersion7().ToString(),
        Limit: 10,
        ResourceTag: resource,
        ExpiresAt: DateTimeOffset.UtcNow.AddMinutes(10),
        Direction: PaginationDirection.Next);

    public static async Task<string> EncodeAsync(IServiceProvider services, CursorPayload payload)
    {
        CursorEncoder encoder = services.GetRequiredService<CursorEncoder>();
        return await encoder.EncodeAsync(payload);
    }

    public static string QueryWithCursor(string cursor, string suffix = "") =>
        $"?cursor={Uri.EscapeDataString(cursor)}{suffix}";

    public static async Task<DefaultModelBindingContext> BindAsync(
        string query,
        string action = nameof(BinderActions.PublicEditais),
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
        string action,
        IDictionary<string, object?>? routeValues = null)
    {
        DefaultHttpContext httpContext = new() { RequestServices = services };
        httpContext.Request.QueryString = new QueryString(query);
        ControllerActionDescriptor descriptor = DescribeAction(action);
        RouteData route = new(new RouteValueDictionary(routeValues));

        return new DefaultModelBindingContext
        {
            ActionContext = new Microsoft.AspNetCore.Mvc.ActionContext(httpContext, route, descriptor),
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

    private static ControllerActionDescriptor DescribeAction(string action)
    {
        ParameterInfo parameter = typeof(BinderActions)
            .GetMethod(action, BindingFlags.Static | BindingFlags.NonPublic)!
            .GetParameters()[0];

        return new ControllerActionDescriptor
        {
            Parameters = new List<ParameterDescriptor> { DescribeParameter(parameter) },
            ActionName = action,
        };
    }

    private static ControllerParameterDescriptor DescribeParameter(ParameterInfo parameter) => new()
    {
        Name = parameter.Name!,
        ParameterType = parameter.ParameterType,
        ParameterInfo = parameter,
    };

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

internal static class BinderActions
{
    internal static void PublicEditais([FromCursor("editais")] PageRequest request) => _ = request;

    internal static void UserInscricoes(
        [FromCursor("inscricoes", RequireUserBinding = true)] PageRequest request) => _ = request;

    internal static void SortedCidades(
        [FromCursor("cidades", RequireSortKey = true)] PageRequest request) => _ = request;

    internal static void UserSortedCidades(
        [FromCursor("cidades", RequireUserBinding = true, RequireSortKey = true)] PageRequest request) => _ = request;

    internal static void ScopedByEntity(
        [FromCursor("atos", ScopeRouteValues = ["entidadeTipo", "entidadeId"])] PageRequest request) => _ = request;

    internal static void WithoutAttribute(PageRequest request) => _ = request;
}
