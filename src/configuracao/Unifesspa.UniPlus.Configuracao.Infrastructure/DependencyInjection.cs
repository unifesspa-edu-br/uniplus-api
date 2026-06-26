namespace Unifesspa.UniPlus.Configuracao.Infrastructure;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// Registra a infraestrutura do módulo Configuracao (DbContext + interceptors
/// transversais soft delete + audit + repositórios dos cadastros Campus e
/// LocalOferta, UNI-REQ #587). Wire-up centralizado em
/// <see cref="UniPlusDbContextOptionsExtensions"/> (ADR-0054).
/// </summary>
public static class ConfiguracaoInfrastructureRegistration
{
    private const string ConnectionStringName = "ConfiguracaoDb";

    public static IServiceCollection AddConfiguracaoInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddUniPlusEfInterceptors();

        services.AddDbContext<ConfiguracaoDbContext>((serviceProvider, options) =>
            options.UseUniPlusNpgsqlConventions<ConfiguracaoDbContext>(
                serviceProvider, ConnectionStringName, schema: ConfiguracaoDbContext.Schema));

        services.AddScoped<IConfiguracaoUnitOfWork>(serviceProvider =>
            serviceProvider.GetRequiredService<ConfiguracaoDbContext>());

        services.AddScoped<ICampusRepository, CampusRepository>();
        services.AddScoped<ILocalOfertaRepository, LocalOfertaRepository>();
        services.AddScoped<IReferenciaReservaDemograficaRepository, ReferenciaReservaDemograficaRepository>();
        services.AddScoped<IPesoAreaEnemRepository, PesoAreaEnemRepository>();

        // Readers cross-módulo (ADR-0056).
        services.AddScoped<IReferenciaReservaDemograficaReader, ReferenciaReservaDemograficaReader>();
        services.AddScoped<IPesoAreaEnemReader, PesoAreaEnemReader>();

        return services;
    }
}
