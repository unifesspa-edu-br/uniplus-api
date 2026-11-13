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

        RegistrarOpcoes(services, configuration);
        RegistrarCodificadorDoCursor(services);
        RegistrarFalhasDeBindingComoProblema(services);

        return services;
    }

    /// <summary>
    /// Sem regras, <c>ValidateOnStart</c> é no-op e uma configuração inconsistente (<c>LimitMin &gt; LimitMax</c>, por
    /// exemplo) só falharia em runtime dentro do <c>Math.Clamp</c> do binder, devolvendo 500.
    /// </summary>
    private static void RegistrarOpcoes(IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<CursorPaginationOptions>()
            .Bind(configuration.GetSection(CursorPaginationOptions.SectionName))
            .Validate(static o => o.LimitMin >= 1, "CursorPaginationOptions.LimitMin deve ser >= 1.")
            .Validate(static o => o.LimitMax >= o.LimitMin, "CursorPaginationOptions.LimitMax deve ser >= LimitMin.")
            .Validate(static o => o.LimitDefault >= o.LimitMin && o.LimitDefault <= o.LimitMax,
                "CursorPaginationOptions.LimitDefault deve estar entre LimitMin e LimitMax.")
            .Validate(static o => o.CursorTtl > TimeSpan.Zero, "CursorPaginationOptions.CursorTtl deve ser positivo.")
            .Validate(static o => !string.IsNullOrWhiteSpace(o.KeyName), "CursorPaginationOptions.KeyName é obrigatório.")
            .ValidateOnStart();
    }

    /// <summary>
    /// <c>TryAdd</c> respeita o <see cref="TimeProvider"/> já registrado pelo host (testes determinísticos, relógio
    /// controlado, replay); <c>AddSingleton</c> direto sobrescreveria esse override em silêncio.
    /// </summary>
    private static void RegistrarCodificadorDoCursor(IServiceCollection services)
    {
        services.AddSingleton<CursorEncoder>();
        services.TryAddSingleton(TimeProvider.System);
    }

    /// <summary>
    /// Compõe com o factory padrão do MVC: tenta primeiro o erro de binding de cursor e, se não for dele, delega ao
    /// comportamento anterior.
    /// </summary>
    private static void RegistrarFalhasDeBindingComoProblema(IServiceCollection services)
    {
        services.AddSingleton<Errors.IDomainErrorRegistration, PaginationDomainErrorRegistration>();

        services.PostConfigure<ApiBehaviorOptions>(options =>
        {
            Func<ActionContext, IActionResult> previous = options.InvalidModelStateResponseFactory;
            options.InvalidModelStateResponseFactory = context =>
                CursorPaginationProblemFactory.TryBuild(context) ?? previous(context);
        });
    }
}
