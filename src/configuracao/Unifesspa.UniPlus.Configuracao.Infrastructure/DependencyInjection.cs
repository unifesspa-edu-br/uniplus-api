namespace Unifesspa.UniPlus.Configuracao.Infrastructure;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;

/// <summary>
/// Registra a infraestrutura do módulo Configuracao (DbContext + interceptors
/// transversais soft delete + audit). Repositórios e readers entram em F2 com
/// as entidades de catálogo.
/// </summary>
public static class ConfiguracaoInfrastructureRegistration
{
    private const string ConnectionStringName = "ConfiguracaoDb";

    public static IServiceCollection AddConfiguracaoInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddUniPlusEfInterceptors();

        services.AddDbContext<ConfiguracaoDbContext>((serviceProvider, options) =>
            options.UseUniPlusNpgsqlConventions<ConfiguracaoDbContext>(serviceProvider, ConnectionStringName));

        services.AddScoped<IUnitOfWork>(serviceProvider =>
            serviceProvider.GetRequiredService<ConfiguracaoDbContext>());

        return services;
    }
}
