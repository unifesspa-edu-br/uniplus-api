namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Domain.Entities;

/// <summary>
/// Configuração EF Core da tabela append-only <c>snapshot_publicacao</c>
/// (RN08, ADR-0100, Story #759 T4 #785). Mesmo padrão de
/// <c>ObrigatoriedadeLegalHistoricoConfiguration</c>: sem soft-delete, sem
/// audit fields, sem updates — qualquer mutação fora de <c>INSERT</c> é
/// incidente operacional.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class SnapshotPublicacaoConfiguration : IEntityTypeConfiguration<SnapshotPublicacao>
{
    private const int HashLength = 64;
    private const int SchemaVersionMaxLength = 20;
    private const int AlgoritmoHashMaxLength = 60;
    private const int AtorUsuarioSubMaxLength = 255;

    public void Configure(EntityTypeBuilder<SnapshotPublicacao> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("snapshot_publicacao");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.EditalId).IsRequired();

        builder.Property(s => s.SchemaVersion)
            .HasMaxLength(SchemaVersionMaxLength)
            .IsRequired();

        builder.Property(s => s.AlgoritmoHash)
            .HasMaxLength(AlgoritmoHashMaxLength)
            .IsRequired();

        // Bytes canônicos (ADR-0100 item 6) — base do hash; fonte única de verdade.
        builder.Property(s => s.ConfiguracaoCongeladaCanonica)
            .HasColumnType("bytea")
            .IsRequired();

        // Derivado por parsing UTF-8 dos bytes canônicos — só consulta SQL
        // (ADR-0100 item 7). O banco não re-serializa nem reordena chaves.
        builder.Property(s => s.ConfiguracaoCongelada)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(s => s.HashConfiguracao)
            .HasMaxLength(HashLength)
            .IsFixedLength()
            .IsRequired();

        builder.Property(s => s.HashEdital)
            .HasMaxLength(HashLength)
            .IsFixedLength()
            .IsRequired();

        builder.Property(s => s.AtorUsuarioSub)
            .HasMaxLength(AtorUsuarioSubMaxLength)
            .IsRequired();

        builder.Property(s => s.DataPublicacao)
            .HasDefaultValueSql("now()")
            .IsRequired();

        // FK para editais — UNIQUE (1 snapshot por Edital). Sem nav property
        // — a forensic não navega de volta, mesmo padrão de
        // ObrigatoriedadeLegalHistorico. Nome explícito: o derivado pelo EF
        // estoura o limite de 63 chars do PostgreSQL.
        builder.HasOne<Edital>()
            .WithOne()
            .HasForeignKey<SnapshotPublicacao>(s => s.EditalId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_snapshot_publicacao_edital_id");

        builder.HasIndex(s => s.EditalId)
            .IsUnique()
            .HasDatabaseName("ux_snapshot_publicacao_edital_id");
    }
}
