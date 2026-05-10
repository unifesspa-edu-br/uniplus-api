namespace Unifesspa.UniPlus.Ingresso.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Interceptors;
using Domain.Interfaces;
using Persistence;
using Persistence.Repositories;

public static class IngressoInfrastructureRegistration
{
    private const string ConnectionStringName = "IngressoDb";

    /// <summary>
    /// Registra a infraestrutura do módulo Ingresso (DbContext + interceptors +
    /// repositórios). A connection string é lida do <see cref="IConfiguration"/>
    /// injetado no factory do <c>AddDbContext</c> — alinhado com o padrão lazy
    /// do <c>UseWolverineOutboxCascading</c> (issue #204). Test hosts que
    /// sobrescrevem <c>ConnectionStrings:IngressoDb</c> via env var ou
    /// <c>InMemoryCollection</c> ganham o override automaticamente, sem
    /// precisar re-registrar o DbContext.
    /// </summary>
    public static IServiceCollection AddIngressoInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // SoftDeleteInterceptor consome IUserContext scoped (HttpUserContext)
        // para preencher DeletedBy com o usuário autenticado (issue #127).
        // Scoped acompanha o ciclo de vida do request — Singleton aqui causaria
        // captive dependency com IUserContext.
        services.AddScoped<SoftDeleteInterceptor>();
        services.AddSingleton<AuditableInterceptor>();

        services.AddDbContext<IngressoDbContext>((serviceProvider, options) =>
        {
            IConfiguration configuration = serviceProvider.GetRequiredService<IConfiguration>();
            string? connectionString = configuration.GetConnectionString(ConnectionStringName);
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    $"ConnectionStrings:{ConnectionStringName} não configurada — defina via appsettings ou env var "
                    + $"`ConnectionStrings__{ConnectionStringName}`. Valores vazios/whitespace também são rejeitados.");
            }

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
