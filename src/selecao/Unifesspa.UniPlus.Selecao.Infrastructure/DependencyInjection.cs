namespace Unifesspa.UniPlus.Selecao.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Interceptors;
using Domain.Interfaces;
using ExternalServices;
using Persistence;
using Persistence.Repositories;

public static class SelecaoInfrastructureRegistration
{
    private const string ConnectionStringName = "SelecaoDb";

    /// <summary>
    /// Registra a infraestrutura do módulo Seleção (DbContext + interceptors +
    /// repositórios + serviços externos). A connection string é lida do
    /// <see cref="IConfiguration"/> injetado no factory do <c>AddDbContext</c>
    /// — alinhado com o padrão lazy do <c>UseWolverineOutboxCascading</c>
    /// (issue #204). Test hosts que sobrescrevem <c>ConnectionStrings:SelecaoDb</c>
    /// via env var ou <c>InMemoryCollection</c> ganham o override automaticamente,
    /// sem precisar re-registrar o DbContext.
    /// </summary>
    public static IServiceCollection AddSelecaoInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<SoftDeleteInterceptor>();
        services.AddSingleton<AuditableInterceptor>();

        services.AddDbContext<SelecaoDbContext>((serviceProvider, options) =>
        {
            IConfiguration configuration = serviceProvider.GetRequiredService<IConfiguration>();
            string connectionString = configuration.GetConnectionString(ConnectionStringName)
                ?? throw new InvalidOperationException(
                    $"ConnectionStrings:{ConnectionStringName} não configurada — defina via appsettings ou env var "
                    + $"`ConnectionStrings__{ConnectionStringName}`.");

            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(typeof(SelecaoDbContext).Assembly.FullName);

                // EnableRetryOnFailure é deliberadamente NÃO configurado aqui:
                // o NpgsqlRetryingExecutionStrategy é incompatível com as
                // user-initiated transactions abertas pela política
                // AutoApplyTransactions + EnrollDbContextInTransaction do
                // Wolverine outbox (ver WolverineOutboxConfiguration e
                // CascadingApiFactory). Resiliência a falhas transientes de
                // conexão fica a cargo das policies de retry do Wolverine no
                // nível do envelope, não do EF Core.
            });

            options.AddInterceptors(
                serviceProvider.GetRequiredService<SoftDeleteInterceptor>(),
                serviceProvider.GetRequiredService<AuditableInterceptor>());
        });

        services.AddScoped<IUnitOfWork>(serviceProvider =>
            serviceProvider.GetRequiredService<SelecaoDbContext>());

        services.AddScoped<IEditalRepository, EditalRepository>();
        services.AddScoped<IInscricaoRepository, InscricaoRepository>();
        services.AddScoped<IGovBrAuthService, GovBrAuthService>();

        return services;
    }
}
