namespace Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Pagination;

/// <summary>
/// Registra o stack de cursor pagination (ADR-0026 + ADR-0031): options,
/// <see cref="CursorEncoder"/>, <see cref="PageRequestModelBinder"/> e o
/// hook <c>InvalidModelStateResponseFactory</c> que mapeia falhas de
/// binding para <c>ProblemDetails</c> (400/410/422) via <see cref="Unifesspa.UniPlus.Infrastructure.Core.Errors.IDomainErrorMapper"/>.
/// </summary>
public static class CursorPaginationServiceCollectionExtensions
{
    public static IServiceCollection AddCursorPagination(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<CursorPaginationOptions>()
            .Bind(configuration.GetSection(CursorPaginationOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<CursorEncoder>();
        services.AddSingleton(TimeProvider.System);

        services.PostConfigure<ApiBehaviorOptions>(options =>
        {
            // Compõe com o factory default: tenta cursor binding error primeiro;
            // se não for nosso, delega para o comportamento padrão MVC.
            Func<ActionContext, IActionResult> previous = options.InvalidModelStateResponseFactory;
            options.InvalidModelStateResponseFactory = context =>
                CursorPaginationProblemFactory.TryBuild(context) ?? previous(context);
        });

        return services;
    }
}
