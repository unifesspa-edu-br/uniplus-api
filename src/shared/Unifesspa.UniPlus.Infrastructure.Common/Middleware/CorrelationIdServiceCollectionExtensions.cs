namespace Unifesspa.UniPlus.Infrastructure.Common.Middleware;

using Microsoft.Extensions.DependencyInjection;

public static class CorrelationIdServiceCollectionExtensions
{
    public static IServiceCollection AddCorrelationIdAccessor(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Registrar a implementação concreta como singleton e resolver as duas
        // interfaces para a MESMA instância. ICorrelationIdAccessor fica
        // disponível amplamente (read-only); ICorrelationIdWriter é usado
        // apenas pelo middleware.
        services.AddSingleton<CorrelationIdAccessor>();
        services.AddSingleton<ICorrelationIdAccessor>(sp => sp.GetRequiredService<CorrelationIdAccessor>());
        services.AddSingleton<ICorrelationIdWriter>(sp => sp.GetRequiredService<CorrelationIdAccessor>());
        return services;
    }
}
