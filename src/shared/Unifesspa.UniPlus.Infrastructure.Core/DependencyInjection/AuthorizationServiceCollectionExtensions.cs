namespace Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Authorization;

/// <summary>
/// Registra a autorização baseada em áreas organizacionais (ADR-0057): o
/// <see cref="AreaScopedAuthorizationHandler"/> e a policy nomeada
/// <see cref="AreaScopedPolicies.RequireAreaProprietario"/>.
/// </summary>
/// <remarks>
/// A policy é baseada em RECURSO — os controllers admin de configuração (F2)
/// invocam imperativamente, passando o <c>IAreaScopedEntity</c> carregado:
/// <code>
/// await authorizationService.AuthorizeAsync(
///     User, entidadeAreaScoped, AreaScopedPolicies.RequireAreaProprietario);
/// </code>
/// Não há <c>[Authorize(Policy = ...)]</c> declarativo porque o atributo não
/// carrega o recurso que o handler precisa para decidir.
/// </remarks>
public static class AuthorizationServiceCollectionExtensions
{
    public static IServiceCollection AddAreaScopedAuthorization(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Scoped: o handler depende de IUserContext (HttpUserContext), Scoped
        // por ciclo de request.
        services.AddScoped<IAuthorizationHandler, AreaScopedAuthorizationHandler>();

        // RequireAuthenticatedUser antes do requirement de recurso: principal
        // anônimo é barrado na policy, sem chegar ao AreaScopedAuthorizationHandler.
        services.AddAuthorizationBuilder()
            .AddPolicy(
                AreaScopedPolicies.RequireAreaProprietario,
                policy => policy
                    .RequireAuthenticatedUser()
                    .AddRequirements(new RequireAreaProprietarioRequirement()));

        return services;
    }
}
