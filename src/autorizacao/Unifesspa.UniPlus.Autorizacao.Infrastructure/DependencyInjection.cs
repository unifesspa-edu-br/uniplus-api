namespace Unifesspa.UniPlus.Autorizacao.Infrastructure;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// Registra a infraestrutura do módulo Autorização (DbContext, interceptors, repositório e a
/// porta do catálogo). Wire-up centralizado em <see cref="UniPlusDbContextOptionsExtensions"/>
/// (ADR-0054).
/// </summary>
public static class AutorizacaoInfrastructureRegistration
{
    private const string ConnectionStringName = "AutorizacaoDb";

    public static IServiceCollection AddAutorizacaoInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddUniPlusEfInterceptors();

        services.AddDbContext<AutorizacaoDbContext>((serviceProvider, options) =>
            options.UseUniPlusNpgsqlConventions<AutorizacaoDbContext>(serviceProvider, ConnectionStringName, schema: AutorizacaoDbContext.Schema));

        // A Application consome a abstração do próprio módulo, roteada para o DbContext (ADR-0042).
        services.AddScoped<IAutorizacaoUnitOfWork>(serviceProvider =>
            serviceProvider.GetRequiredService<AutorizacaoDbContext>());

        services.AddScoped<IPerfilRepository, PerfilRepository>();
        services.AddScoped<IConcessaoRepository, ConcessaoRepository>();
        services.AddSingleton<ICatalogoDePermissoesConsultavel, CatalogoDePermissoesDaAplicacao>();

        return services;
    }
}
