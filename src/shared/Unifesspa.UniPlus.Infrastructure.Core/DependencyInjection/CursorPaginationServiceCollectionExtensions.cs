namespace Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
            // Validators explícitos: ValidateOnStart sem regras é no-op e config
            // inconsistente (LimitMin > LimitMax, etc.) só falharia em runtime
            // dentro do Math.Clamp do binder, devolvendo 500.
            .Validate(static o => o.LimitMin >= 1, "CursorPaginationOptions.LimitMin deve ser >= 1.")
            .Validate(static o => o.LimitMax >= o.LimitMin, "CursorPaginationOptions.LimitMax deve ser >= LimitMin.")
            .Validate(static o => o.LimitDefault >= o.LimitMin && o.LimitDefault <= o.LimitMax,
                "CursorPaginationOptions.LimitDefault deve estar entre LimitMin e LimitMax.")
            .Validate(static o => o.CursorTtl > TimeSpan.Zero, "CursorPaginationOptions.CursorTtl deve ser positivo.")
            .ValidateOnStart();

        services.AddSingleton<CursorEncoder>();
        // TryAdd: respeita TimeProvider já registrado pelo host (testes
        // determinísticos, hosts com clock controlado, replay). AddSingleton
        // direto sobrescreveria silenciosamente esse override.
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<Errors.IDomainErrorRegistration, PaginationDomainErrorRegistration>();

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
