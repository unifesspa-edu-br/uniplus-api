namespace Unifesspa.UniPlus.Ingresso.Infrastructure;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Abstractions;
using Domain.Interfaces;
using Persistence;
using Persistence.Repositories;

public static class IngressoInfrastructureRegistration
{
    private const string ConnectionStringName = "IngressoDb";

    /// <summary>
    /// Registra a infraestrutura do módulo Ingresso (DbContext + interceptors +
    /// repositórios). Wire-up centralizado em
    /// <see cref="UniPlusDbContextOptionsExtensions"/> (ADR-0054).
    /// </summary>
    public static IServiceCollection AddIngressoInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddUniPlusEfInterceptors();

        services.AddDbContext<IngressoDbContext>((serviceProvider, options) =>
            options.UseUniPlusNpgsqlConventions<IngressoDbContext>(serviceProvider, ConnectionStringName, schema: IngressoDbContext.Schema));

        services.AddScoped<IIngressoUnitOfWork>(serviceProvider =>
            serviceProvider.GetRequiredService<IngressoDbContext>());

        services.AddScoped<IChamadaRepository, ChamadaRepository>();
        services.AddScoped<IMatriculaRepository, MatriculaRepository>();

        return services;
    }
}
