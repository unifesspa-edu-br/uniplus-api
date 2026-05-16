namespace Unifesspa.UniPlus.Selecao.Infrastructure;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Domain.Interfaces;
using ExternalServices;
using Persistence;
using Persistence.Interceptors;
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

        // ObrigatoriedadeLegalHistoricoInterceptor (Story #460, ADR-0058) é
        // scoped por simetria com SoftDelete/Auditable — depende do
        // IUserContext scoped para preencher snapshot_by.
        services.AddScoped<ObrigatoriedadeLegalHistoricoInterceptor>();

        services.AddDbContext<SelecaoDbContext>((serviceProvider, options) =>
        {
            options.UseUniPlusNpgsqlConventions<SelecaoDbContext>(serviceProvider, ConnectionStringName);

            // Encaixe deliberado em sequência aos interceptors cross-cutting do
            // UseUniPlusNpgsqlConventions — roda DEPOIS de SoftDelete + Auditable,
            // garantindo que mutações via soft-delete (Delete convertido para
            // Modified+IsDeleted=true) também gerem linha no histórico.
            options.AddInterceptors(
                serviceProvider.GetRequiredService<ObrigatoriedadeLegalHistoricoInterceptor>());
        });

        services.AddScoped<IUnitOfWork>(serviceProvider =>
            serviceProvider.GetRequiredService<SelecaoDbContext>());

        services.AddScoped<IEditalRepository, EditalRepository>();
        services.AddScoped<IInscricaoRepository, InscricaoRepository>();
        services.AddScoped<IGovBrAuthService, GovBrAuthService>();

        return services;
    }
}
