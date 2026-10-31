namespace Unifesspa.UniPlus.Infrastructure.Core.HealthChecks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;

using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;

public static class HealthCheckEndpointsExtensions
{
    /// <summary>
    /// Mapeia as sondas de saúde, públicas porque o orquestrador as chama sem token. <c>/health/live</c> não avalia
    /// nenhuma verificação (200 enquanto o processo responde, sem reiniciar o pod quando uma dependência cai);
    /// <c>/health/ready</c> agrega as verificações marcadas com <see cref="HealthChecksServiceCollectionExtensions.ReadyTag"/>;
    /// <c>/health</c> agrega todas.
    /// </summary>
    public static IEndpointRouteBuilder MapUniPlusHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = static _ => false })
            .AllowAnonymous();
        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = static h => h.Tags.Contains(HealthChecksServiceCollectionExtensions.ReadyTag),
        }).AllowAnonymous();
        endpoints.MapHealthChecks("/health").AllowAnonymous();

        return endpoints;
    }
}
