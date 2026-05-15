namespace Unifesspa.UniPlus.Parametrizacao.Infrastructure;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Parametrizacao.Infrastructure.Persistence;

/// <summary>
/// Registra a infraestrutura do módulo Parametrizacao (DbContext + interceptors
/// transversais soft delete + audit). Repositórios e readers entram em F2 com
/// as entidades de catálogo.
/// </summary>
public static class ParametrizacaoInfrastructureRegistration
{
    private const string ConnectionStringName = "ParametrizacaoDb";

    public static IServiceCollection AddParametrizacaoInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddUniPlusEfInterceptors();

        services.AddDbContext<ParametrizacaoDbContext>((serviceProvider, options) =>
            options.UseUniPlusNpgsqlConventions<ParametrizacaoDbContext>(serviceProvider, ConnectionStringName));

        services.AddScoped<IUnitOfWork>(serviceProvider =>
            serviceProvider.GetRequiredService<ParametrizacaoDbContext>());

        return services;
    }
}
