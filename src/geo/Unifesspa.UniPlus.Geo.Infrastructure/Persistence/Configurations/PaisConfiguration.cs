namespace Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Geo.Domain.Entities;

/// <summary>
/// Mapeamento de <see cref="Pais"/> (topo da hierarquia). Reference data sem
/// soft-delete (ADR-0092): chave natural <c>sigla_iso</c> UNIQUE sustenta o upsert
/// idempotente do ETL — o ETL atualiza a mesma linha por chave natural (não há
/// histórico por versão), então o UNIQUE é global e correto.
/// </summary>
/// <remarks>
/// Strings de origem externa ficam <c>text</c> (sem <c>HasMaxLength</c>) — a carga
/// em lote do DNE é tolerante por construção; só <c>versao_dataset</c> (que o ETL
/// controla) tem teto.
/// </remarks>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class PaisConfiguration : IEntityTypeConfiguration<Pais>
{
    public void Configure(EntityTypeBuilder<Pais> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("pais");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.SiglaIso).IsRequired();
        builder.Property(p => p.Sigla).IsRequired();
        builder.Property(p => p.Nome).IsRequired();

        builder.HasIndex(p => p.SiglaIso)
            .IsUnique()
            .HasDatabaseName("ix_pais_sigla_iso");

        builder.ConfigurarProveniencia(p => p.VersaoDataset, p => p.Vigente);
        builder.HasIndex(p => p.VersaoDataset)
            .HasDatabaseName("ix_pais_versao_dataset");
    }
}
