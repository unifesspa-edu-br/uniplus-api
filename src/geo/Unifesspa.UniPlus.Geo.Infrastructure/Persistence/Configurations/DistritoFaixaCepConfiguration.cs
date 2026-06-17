namespace Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Geo.Domain.Entities;

/// <summary>
/// Mapeamento de <see cref="DistritoFaixaCep"/>. FK intra-banco para <c>distrito</c>;
/// chave natural <c>(distrito_id, cep_inicial, cep_final)</c> UNIQUE (idempotência do
/// ETL; cobre a FK e o lookup por distrito). CHECK/sobreposição ficam no ETL/lookup
/// (carga tolerante, ADR-0092).
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class DistritoFaixaCepConfiguration : IEntityTypeConfiguration<DistritoFaixaCep>
{
    public void Configure(EntityTypeBuilder<DistritoFaixaCep> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("distrito_faixa_cep");
        builder.HasKey(f => f.Id);

        builder.Property(f => f.CepInicial).IsRequired();
        builder.Property(f => f.CepFinal).IsRequired();

        builder.HasOne<Distrito>()
            .WithMany()
            .HasForeignKey(f => f.DistritoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(f => new { f.DistritoId, f.CepInicial, f.CepFinal })
            .IsUnique()
            .HasDatabaseName("ix_distrito_faixa_cep_natural");

        builder.ConfigurarProveniencia(f => f.VersaoDataset, f => f.Vigente);
        builder.HasIndex(f => f.VersaoDataset)
            .HasDatabaseName("ix_distrito_faixa_cep_versao_dataset");
    }
}
