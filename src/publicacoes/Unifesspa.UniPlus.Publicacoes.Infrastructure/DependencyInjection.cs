namespace Unifesspa.UniPlus.Publicacoes.Infrastructure;

using Microsoft.Extensions.DependencyInjection;

using Persistence;
using Persistence.Repositories;

using Readers;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Publicacoes.Application.Abstractions;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Publicacoes.Domain.Interfaces;

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

        // Leitura cross-módulo do catálogo (ADR-0056): deixa o domínio que vai publicar
        // conferir o tipo declarado ANTES de escrever, em vez de descobrir na dead letter.
        services.AddScoped<ITipoAtoPublicadoReader, TipoAtoPublicadoReader>();
        services.AddScoped<IVagaDeLinhagemReader, VagaDeLinhagemReader>();
        services.AddScoped<IAtoNormativoRepository, AtoNormativoRepository>();

        return services;
    }
}
