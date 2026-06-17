namespace Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Geo.Domain.Entities;

/// <summary>
/// Mapeamento de <see cref="Cidade"/> (eixo central do Geo). Chave natural
/// <c>codigo_ibge</c> UNIQUE (upsert in place do ETL); homônimos em UFs distintas
/// coexistem. FK intra-banco para <c>estado</c> (ADR-0054), coordenada GIST
/// (ADR-0091). O índice trigram (<c>gin_trgm_ops</c>) em <c>nome_normalizado</c>
/// prepara o autocomplete acento-insensível da F4 — exige a extensão <c>pg_trgm</c>
/// (declarada no <see cref="GeoDbContext"/>).
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class CidadeConfiguration : IEntityTypeConfiguration<Cidade>
{
    public void Configure(EntityTypeBuilder<Cidade> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("cidade");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Uf).IsRequired();
        builder.Property(c => c.CodigoIbge).IsRequired();
        builder.Property(c => c.Nome).IsRequired();
        builder.Property(c => c.Latitude).HasPrecision(9, 6);
        builder.Property(c => c.Longitude).HasPrecision(9, 6);
        builder.ConfigurarCoordenada(c => c.Coordenada);

        builder.HasIndex(c => c.CodigoIbge)
            .IsUnique()
            .HasDatabaseName("ix_cidade_codigo_ibge");

        builder.HasOne<Estado>()
            .WithMany()
            .HasForeignKey(c => c.EstadoId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(c => c.EstadoId)
            .HasDatabaseName("ix_cidade_estado_id");

        builder.HasIndex(c => c.Coordenada)
            .HasMethod("gist")
            .HasDatabaseName("ix_cidade_coordenada");

        // Índice trigram para autocomplete acento-insensível (F4): o termo é
        // normalizado no app e comparado com nome_normalizado ILIKE — sem
        // immutable_unaccent em runtime. Requer extensão pg_trgm.
        builder.HasIndex(c => c.NomeNormalizado)
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops")
            .HasDatabaseName("ix_cidade_nome_normalizado_trgm");

        builder.ConfigurarProveniencia(c => c.VersaoDataset, c => c.Vigente);
        builder.HasIndex(c => c.VersaoDataset)
            .HasDatabaseName("ix_cidade_versao_dataset");
    }
}
