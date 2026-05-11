namespace Unifesspa.UniPlus.Portal.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Interceptors;
using Persistence;

public static class PortalInfrastructureRegistration
{
    private const string ConnectionStringName = "PortalDb";

    /// <summary>
    /// Registra a infraestrutura do módulo Portal (DbContext + interceptors).
    /// A connection string é lida do <see cref="IConfiguration"/> injetado no
    /// factory do <c>AddDbContext</c> — alinhado com o padrão lazy do
    /// <c>UseWolverineOutboxCascading</c> (issue #204) e simétrico ao
    /// Selecao/Ingresso. Test hosts que sobrescrevem
    /// <c>ConnectionStrings:PortalDb</c> via env var ou
    /// <c>InMemoryCollection</c> ganham o override automaticamente.
    /// </summary>
    public static IServiceCollection AddPortalInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // SoftDeleteInterceptor (issue #127) e AuditableInterceptor (issue #390)
        // consomem IUserContext scoped (HttpUserContext) para preencher
        // DeletedBy / CreatedBy / UpdatedBy com o usuário autenticado. Scoped
        // acompanha o ciclo de vida do request — Singleton aqui causaria
        // captive dependency com IUserContext (UserId congelaria no primeiro
        // request servido pelo processo).
        services.AddScoped<SoftDeleteInterceptor>();
        services.AddScoped<AuditableInterceptor>();

        services.AddDbContext<PortalDbContext>((serviceProvider, options) =>
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
                npgsqlOptions.MigrationsAssembly(typeof(PortalDbContext).Assembly.FullName);

                // EnableRetryOnFailure deliberadamente NÃO configurado — mesma razão
                // documentada em IngressoInfrastructureRegistration: incompatível
                // com user-initiated transactions do Wolverine outbox. Resiliência
                // a falhas transientes fica a cargo das policies do Wolverine no
                // nível do envelope.
            });

            options.AddInterceptors(
                serviceProvider.GetRequiredService<SoftDeleteInterceptor>(),
                serviceProvider.GetRequiredService<AuditableInterceptor>());
        });

        services.AddScoped<IUnitOfWork>(serviceProvider =>
            serviceProvider.GetRequiredService<PortalDbContext>());

        return services;
    }
}
