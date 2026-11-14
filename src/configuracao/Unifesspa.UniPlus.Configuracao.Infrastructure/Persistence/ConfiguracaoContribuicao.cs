namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// O que a Configuração acrescenta ao contexto único: as configurações, a extensão de busca por proximidade e as sementes
/// por código que rodam depois das migrations.
/// </summary>
public sealed class ConfiguracaoContribuicao : IContribuicaoDoModulo
{
    public const string NomeDoEsquema = "configuracao";

    public string Esquema => NomeDoEsquema;

    public void AplicarAoModelo(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ConfiguracaoContribuicao).Assembly);

        // Busca por proximidade indexada (GIN trigram) do catálogo de termos de consentimento por nome, sem ILIKE em
        // full scan. O índice de expressão, que o HasIndex fluente não representa, vive no script do módulo.
        modelBuilder.HasPostgresExtension("pg_trgm");
    }

    public Task ExecutarEtapaPosteriorDaMigrationAsync(IServiceProvider servicos, CancellationToken cancellationToken) =>
        SementesDaConfiguracao.AplicarAsync(servicos, cancellationToken);
}
