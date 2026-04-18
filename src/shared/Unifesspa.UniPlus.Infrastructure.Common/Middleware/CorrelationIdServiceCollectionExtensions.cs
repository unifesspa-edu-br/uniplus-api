namespace Unifesspa.UniPlus.Infrastructure.Common.Middleware;

using Microsoft.Extensions.DependencyInjection;

public static class CorrelationIdServiceCollectionExtensions
{
    public static IServiceCollection AddCorrelationIdAccessor(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<ICorrelationIdAccessor, CorrelationIdAccessor>();
        return services;
    }
}
