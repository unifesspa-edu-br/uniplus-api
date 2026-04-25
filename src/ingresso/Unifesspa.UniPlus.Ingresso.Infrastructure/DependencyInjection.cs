namespace Unifesspa.UniPlus.Ingresso.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Interceptors;
using Unifesspa.UniPlus.Ingresso.Domain.Interfaces;
using Unifesspa.UniPlus.Ingresso.Infrastructure.Persistence;
using Unifesspa.UniPlus.Ingresso.Infrastructure.Persistence.Repositories;

using Wolverine.EntityFrameworkCore;

public static class IngressoInfrastructureRegistration
{
    public const string ConnectionStringName = "IngressoDb";
    public const string WolverineSchema = "wolverine";

    public static IServiceCollection AddIngressoInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<SoftDeleteInterceptor>();
        services.AddSingleton<AuditableInterceptor>();

        // AddDbContextWithWolverineIntegration anexa os interceptors do Wolverine
        // ao DbContext: SaveChanges passa a auto-publicar domain events de
        // EntityBase.DomainEvents para wolverine.wolverine_outgoing_envelopes
        // dentro da mesma transação (atomicidade write+evento — issue #135).
        services.AddDbContextWithWolverineIntegration<IngressoDbContext>((serviceProvider, options) =>
        {
            // Resolver lazy via IConfiguration permite que WebApplicationFactory
            // injete overrides (ex.: Testcontainers) antes do DbContext materializar.
            string connectionString = serviceProvider.GetRequiredService<IConfiguration>()
                .GetConnectionString(ConnectionStringName)
                ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' não configurada.");

            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(typeof(IngressoDbContext).Assembly.FullName);
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorCodesToAdd: null);
            });

            options.AddInterceptors(
                serviceProvider.GetRequiredService<SoftDeleteInterceptor>(),
                serviceProvider.GetRequiredService<AuditableInterceptor>());
        }, WolverineSchema);

        services.AddScoped<IUnitOfWork>(serviceProvider =>
            serviceProvider.GetRequiredService<IngressoDbContext>());

        services.AddScoped<IChamadaRepository, ChamadaRepository>();
        services.AddScoped<IMatriculaRepository, MatriculaRepository>();

        return services;
    }
}
