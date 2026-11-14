namespace Unifesspa.UniPlus.Monolito.Persistencia;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>Registro da persistência única do monólito: o contexto, a unidade de trabalho e as migrations da subida.</summary>
public static class MonolitoPersistenciaServiceCollectionExtensions
{
    private const string NomeDaConnectionString = "UniPlusDb";

    /// <summary>
    /// Registra o <see cref="MonolitoDbContext"/> com a connection string <c>UniPlusDb</c>, os interceptadores compartilhados e os de
    /// cada módulo, e as migrations na subida. <see cref="UniPlusDbContext"/> e <see cref="IUnitOfWork"/> encaminham para a MESMA
    /// instância do escopo: uma segunda instância faria o repositório escrever num contexto e a unidade de trabalho confirmar
    /// outro, quebrando a atomicidade entre a gravação e o outbox (ADR-0004).
    /// </summary>
    /// <remarks>
    /// O registro das migrations precede o do Wolverine e o dos hosted services dos módulos: o schema EF precisa estar aplicado
    /// antes de o runtime aceitar o primeiro envelope.
    /// </remarks>
    public static IServiceCollection AddMonolitoPersistencia(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddUniPlusEfInterceptors();
        services.AddDbContext<MonolitoDbContext>(ConfigurarOpcoes);
        services.AddScoped<UniPlusDbContext>(static sp => sp.GetRequiredService<MonolitoDbContext>());
        services.AddScoped<IUnitOfWork>(static sp => sp.GetRequiredService<MonolitoDbContext>());
        services.AddDbContextMigrationsOnStartup<MonolitoDbContext>(
            ScriptsDasContribuicoes.Assemblies(MonolitoDbContext.ContribuicoesDoMonolito), ExecutarEtapasPosterioresDosModulosAsync);

        return services;
    }

    private static void ConfigurarOpcoes(IServiceProvider servicos, DbContextOptionsBuilder opcoes)
    {
        opcoes.UseUniPlusNpgsqlConventions<MonolitoDbContext>(servicos, NomeDaConnectionString, UniPlusDbContext.EsquemaDaPlataforma);
        opcoes.AdicionarInterceptadoresDosModulos(servicos, MonolitoDbContext.ContribuicoesDoMonolito);
    }

    private static async Task ExecutarEtapasPosterioresDosModulosAsync(IServiceProvider servicos, CancellationToken cancellationToken)
    {
        foreach (IContribuicaoDoModulo modulo in MonolitoDbContext.ContribuicoesDoMonolito)
        {
            await modulo.ExecutarEtapaPosteriorDaMigrationAsync(servicos, cancellationToken).ConfigureAwait(false);
        }
    }
}
