namespace Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Geo.Domain.Entities;

/// <summary>
/// Mapeamento da entidade-sonda transitória. Prova fim-a-fim o tipo
/// <c>geography(Point,4326)</c> + índice GIST (ADR-0091) contra o PostGIS real.
/// Removida (tabela + índice) na Story de entidades reais de localidade.
/// </summary>
public sealed class PontoReferenciaSondaConfiguration : IEntityTypeConfiguration<PontoReferenciaSonda>
{
    public void Configure(EntityTypeBuilder<PontoReferenciaSonda> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ponto_referencia_sonda");
        builder.HasKey(e => e.Id);

        // Coordenada geográfica SRID 4326 (WGS84) como geography(Point) — o
        // plugin NetTopologySuite do Npgsql traduz NTS Point <-> geography.
        builder.Property(e => e.Coordenada)
            .HasColumnType("geography (Point, 4326)")
            .IsRequired();

        // Índice espacial GIST sobre a coluna geográfica (opclass do PostGIS).
        builder.HasIndex(e => e.Coordenada)
            .HasMethod("gist")
            .HasDatabaseName("idx_ponto_referencia_sonda_coordenada");
    }
}
