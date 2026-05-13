namespace Unifesspa.UniPlus.Selecao.Infrastructure;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Domain.Interfaces;
using ExternalServices;
using Persistence;
using Persistence.Repositories;

public static class SelecaoInfrastructureRegistration
{
    private const string ConnectionStringName = "SelecaoDb";

    /// <summary>
    /// Registra a infraestrutura do módulo Seleção (DbContext + interceptors +
    /// repositórios + serviços externos). Wire-up centralizado em
    /// <see cref="UniPlusDbContextOptionsExtensions"/> (ADR-0054): convenção
    /// snake_case global, soft delete + audit interceptors, leitura lazy de
    /// connection string.
    /// </summary>
    public static IServiceCollection AddSelecaoInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddUniPlusEfInterceptors();

        services.AddDbContext<SelecaoDbContext>((serviceProvider, options) =>
            options.UseUniPlusNpgsqlConventions<SelecaoDbContext>(serviceProvider, ConnectionStringName));

        services.AddScoped<IUnitOfWork>(serviceProvider =>
            serviceProvider.GetRequiredService<SelecaoDbContext>());

        services.AddScoped<IEditalRepository, EditalRepository>();
        services.AddScoped<IInscricaoRepository, InscricaoRepository>();
        services.AddScoped<IGovBrAuthService, GovBrAuthService>();

        return services;
    }
}
