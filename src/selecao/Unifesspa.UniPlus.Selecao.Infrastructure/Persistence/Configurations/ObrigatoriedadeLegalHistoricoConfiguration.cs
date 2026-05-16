namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// Configuração EF Core da tabela append-only
/// <c>obrigatoriedade_legal_historico</c> (CA-03 da Story #460). Sem
/// soft-delete, sem audit fields, sem updates — qualquer mutação fora de
/// <c>INSERT</c> é incidente operacional.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class ObrigatoriedadeLegalHistoricoConfiguration : IEntityTypeConfiguration<ObrigatoriedadeLegalHistorico>
{
    private const int HashLength = 64;
    private const int SnapshotByMaxLength = 255;

    public void Configure(EntityTypeBuilder<ObrigatoriedadeLegalHistorico> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("obrigatoriedade_legal_historico");
        builder.HasKey(h => h.Id);

        builder.Property(h => h.RegraId).IsRequired();

        builder.Property(h => h.ConteudoJson)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(h => h.Hash)
            .HasMaxLength(HashLength)
            .IsFixedLength()
            .IsRequired();

        builder.Property(h => h.SnapshotAt).IsRequired();

        builder.Property(h => h.SnapshotBy)
            .HasMaxLength(SnapshotByMaxLength)
            .IsRequired();

        // Consulta "evolução desta regra" é o caso principal — index por
        // (regra_id, snapshot_at DESC) suporta paginação descendente.
        builder.HasIndex(h => new { h.RegraId, h.SnapshotAt })
            .IsDescending(false, true)
            .HasDatabaseName("ix_obrigatoriedade_legal_historico_regra_snapshot_at");
    }
}
