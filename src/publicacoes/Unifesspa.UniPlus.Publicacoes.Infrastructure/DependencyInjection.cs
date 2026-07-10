namespace Unifesspa.UniPlus.Publicacoes.Infrastructure;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Publicacoes.Application.Abstractions;
using Unifesspa.UniPlus.Publicacoes.Domain.Interfaces;
using Persistence;
using Persistence.Repositories;

public static class PublicacoesInfrastructureRegistration
{
    private const string ConnectionStringName = "PublicacoesDb";

    /// <summary>
    /// Registra a infraestrutura do módulo Publicações (DbContext, interceptors,
    /// unit of work e repositórios). Wire-up centralizado em
    /// <see cref="UniPlusDbContextOptionsExtensions"/>.
    /// </summary>
    public static IServiceCollection AddPublicacoesInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddUniPlusEfInterceptors();

        services.AddDbContext<PublicacoesDbContext>((serviceProvider, options) =>
            options.UseUniPlusNpgsqlConventions<PublicacoesDbContext>(serviceProvider, ConnectionStringName, schema: PublicacoesDbContext.Schema));

        // Forwarding para a MESMA instância do DbContext do escopo. Registrar
        // AddScoped<IPublicacoesUnitOfWork, PublicacoesDbContext>() criaria uma segunda
        // instância por escopo e quebraria a atomicidade write+evento do outbox (ADR-0004).
        services.AddScoped<IPublicacoesUnitOfWork>(serviceProvider =>
            serviceProvider.GetRequiredService<PublicacoesDbContext>());

        services.AddScoped<ITipoAtoPublicadoRepository, TipoAtoPublicadoRepository>();
        services.AddScoped<IAtoNormativoRepository, AtoNormativoRepository>();

        return services;
    }
}
