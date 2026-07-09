namespace Unifesspa.UniPlus.Publicacoes.Infrastructure;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Persistence;

public static class PublicacoesInfrastructureRegistration
{
    private const string ConnectionStringName = "PublicacoesDb";

    /// <summary>
    /// Registra a infraestrutura do módulo Publicações (DbContext + interceptors).
    /// Wire-up centralizado em <see cref="UniPlusDbContextOptionsExtensions"/>.
    ///
    /// Sem repositórios e sem unit of work: o módulo ainda não tem entidades.
    /// </summary>
    public static IServiceCollection AddPublicacoesInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddUniPlusEfInterceptors();

        services.AddDbContext<PublicacoesDbContext>((serviceProvider, options) =>
            options.UseUniPlusNpgsqlConventions<PublicacoesDbContext>(serviceProvider, ConnectionStringName, schema: PublicacoesDbContext.Schema));

        return services;
    }
}
