namespace Unifesspa.UniPlus.Infrastructure.Common.Observability;

using Microsoft.Extensions.DependencyInjection;

using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

public static class OpenTelemetryConfiguration
{
    public static IServiceCollection AdicionarObservabilidade(this IServiceCollection services, string nomeServico)
    {
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(nomeServico))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddSource(nomeServico));

        return services;
    }
}
