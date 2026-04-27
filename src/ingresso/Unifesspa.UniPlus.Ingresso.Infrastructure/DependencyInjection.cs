namespace Unifesspa.UniPlus.Ingresso.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Interceptors;
using Unifesspa.UniPlus.Ingresso.Domain.Interfaces;
using Unifesspa.UniPlus.Ingresso.Infrastructure.Persistence;
using Unifesspa.UniPlus.Ingresso.Infrastructure.Persistence.Repositories;

public static class IngressoInfrastructureRegistration
{
    public static IServiceCollection AddIngressoInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddSingleton<SoftDeleteInterceptor>();
        services.AddSingleton<AuditableInterceptor>();

        services.AddDbContext<IngressoDbContext>((serviceProvider, options) =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(typeof(IngressoDbContext).Assembly.FullName);

                // EnableRetryOnFailure é deliberadamente NÃO configurado aqui:
                // o NpgsqlRetryingExecutionStrategy é incompatível com as
                // user-initiated transactions abertas pela política
                // AutoApplyTransactions + EnrollDbContextInTransaction do
                // Wolverine outbox (ver WolverineOutboxConfiguration).
                // Resiliência a falhas transientes de conexão fica a cargo
                // das policies de retry do Wolverine no nível do envelope,
                // não do EF Core.
            });

            options.AddInterceptors(
                serviceProvider.GetRequiredService<SoftDeleteInterceptor>(),
                serviceProvider.GetRequiredService<AuditableInterceptor>());
        });

        services.AddScoped<IUnitOfWork>(serviceProvider =>
            serviceProvider.GetRequiredService<IngressoDbContext>());

        services.AddScoped<IChamadaRepository, ChamadaRepository>();
        services.AddScoped<IMatriculaRepository, MatriculaRepository>();

        return services;
    }
}
