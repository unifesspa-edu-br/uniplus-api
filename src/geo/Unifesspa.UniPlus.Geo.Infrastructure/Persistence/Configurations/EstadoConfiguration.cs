namespace Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Geo.Domain.Entities;

/// <summary>
/// Mapeamento de <see cref="Estado"/>. Chave natural <c>uf</c> UNIQUE (upsert in
/// place do ETL — uma linha por UF). FK intra-banco para <c>pais</c> (ADR-0054).
/// Coordenada em <c>geography(Point,4326)</c> com índice GIST (ADR-0091); lat/long
/// em <c>numeric(9,6)</c> (≈0,1 m).
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class EstadoConfiguration : IEntityTypeConfiguration<Estado>
{
    public void Configure(EntityTypeBuilder<Estado> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("estado");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Uf).IsRequired();
        builder.Property(e => e.Nome).IsRequired();
        builder.Property(e => e.NomeOrdenacao)
            .IsRequired()
            .HasComputedColumnSql(NomeOrdenacaoSql.Expression, stored: true);
        builder.Property(e => e.Latitude).HasPrecision(9, 6);
        builder.Property(e => e.Longitude).HasPrecision(9, 6);
        builder.ConfigurarCoordenada(e => e.Coordenada);

        builder.HasIndex(e => e.Uf)
            .IsUnique()
            .HasDatabaseName("ix_estado_uf");

        // FK intra-banco para o País (ADR-0054). Restrict cobre o DELETE físico
        // residual; a vigência do reference data é tratada por upsert (ADR-0092),
        // não por exclusão.
        builder.HasOne<Pais>()
            .WithMany()
            .HasForeignKey(e => e.PaisId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => e.PaisId)
            .HasDatabaseName("ix_estado_pais_id");

        builder.HasIndex(e => e.Coordenada)
            .HasMethod("gist")
            .HasDatabaseName("ix_estado_coordenada");

        builder.ConfigurarProveniencia(e => e.VersaoDataset, e => e.Vigente);
        builder.HasIndex(e => e.VersaoDataset)
            .HasDatabaseName("ix_estado_versao_dataset");
    }
}
