namespace Unifesspa.UniPlus.Ingresso.Infrastructure;

using Domain.Interfaces;

using Microsoft.Extensions.DependencyInjection;

using Persistence.Repositories;

public static class IngressoInfrastructureRegistration
{
    /// <summary>
    /// Registra a infraestrutura do módulo Ingresso: os repositórios. O contexto e a unidade de trabalho vêm da
    /// persistência única do monólito.
    /// </summary>
    public static IServiceCollection AddIngressoInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IChamadaRepository, ChamadaRepository>();
        services.AddScoped<IMatriculaRepository, MatriculaRepository>();

        return services;
    }
}
