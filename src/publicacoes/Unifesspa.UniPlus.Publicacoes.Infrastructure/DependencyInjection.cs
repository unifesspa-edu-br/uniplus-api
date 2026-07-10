namespace Unifesspa.UniPlus.Publicacoes.Infrastructure;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Publicacoes.Domain.Interfaces;
using Persistence;
using Persistence.Repositories;

public static class PublicacoesInfrastructureRegistration
{
    private const string ConnectionStringName = "PublicacoesDb";

    /// <summary>
    /// Registra a infraestrutura do módulo Publicações (DbContext, interceptors e
    /// repositórios). Wire-up centralizado em <see cref="UniPlusDbContextOptionsExtensions"/>.
    ///
    /// Sem unit of work: nenhum handler existe ainda para consumi-la.
    /// </summary>
    public static IServiceCollection AddPublicacoesInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddUniPlusEfInterceptors();

        services.AddDbContext<PublicacoesDbContext>((serviceProvider, options) =>
            options.UseUniPlusNpgsqlConventions<PublicacoesDbContext>(serviceProvider, ConnectionStringName, schema: PublicacoesDbContext.Schema));

        services.AddScoped<ITipoAtoPublicadoRepository, TipoAtoPublicadoRepository>();

        return services;
    }
}
