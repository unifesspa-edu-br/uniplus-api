namespace Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Geo.Domain.Entities;

/// <summary>
/// Mapeamento de <see cref="BairroFaixaCep"/>. FK intra-banco para <c>bairro</c>;
/// chave natural <c>(bairro_id, cep_inicial, cep_final)</c> UNIQUE (idempotência do
/// ETL; cobre a FK e o lookup por bairro). CHECK/sobreposição ficam no ETL/lookup
/// (carga tolerante, ADR-0092).
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class BairroFaixaCepConfiguration : IEntityTypeConfiguration<BairroFaixaCep>
{
    public void Configure(EntityTypeBuilder<BairroFaixaCep> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("bairro_faixa_cep");
        builder.HasKey(f => f.Id);

        builder.Property(f => f.CepInicial).IsRequired();
        builder.Property(f => f.CepFinal).IsRequired();

        builder.HasOne<Bairro>()
            .WithMany()
            .HasForeignKey(f => f.BairroId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(f => new { f.BairroId, f.CepInicial, f.CepFinal })
            .IsUnique()
            .HasDatabaseName("ix_bairro_faixa_cep_natural");

        builder.ConfigurarProveniencia(f => f.VersaoDataset, f => f.Vigente);
        builder.HasIndex(f => f.VersaoDataset)
            .HasDatabaseName("ix_bairro_faixa_cep_versao_dataset");
    }
}
