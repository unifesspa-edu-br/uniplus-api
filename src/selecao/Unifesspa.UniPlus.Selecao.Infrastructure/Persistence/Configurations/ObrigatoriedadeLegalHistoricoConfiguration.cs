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

        // FK para obrigatoriedades_legais (regra alvo do snapshot). Sem nav
        // property — a forensic não navega de volta para evitar carregar a
        // regra acidentalmente em queries de histórico. ON DELETE RESTRICT
        // bloqueia hard-delete da regra mãe; o caminho normal é soft-delete
        // (IsDeleted=true, Modified state), que não dispara o RESTRICT.
        // Qualquer DELETE físico (DBA bypass) precisa explicitamente lidar
        // com o histórico antes — invariante de integridade forense.
        // Nome do constraint explícito porque o derivado pelo EF
        // (`fk_obrigatoriedade_legal_historico_obrigatoriedades_legais_regra_id`)
        // estoura o limite de 63 chars do PostgreSQL e é truncado de forma
        // pouco legível.
        builder.HasOne<ObrigatoriedadeLegal>()
            .WithMany()
            .HasForeignKey(h => h.RegraId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_obrigatoriedade_legal_historico_regra_id");

        // Consulta "evolução desta regra" é o caso principal — index por
        // (regra_id, snapshot_at DESC) suporta paginação descendente. Também
        // serve o lookup de FK (regra_id como leading column).
        builder.HasIndex(h => new { h.RegraId, h.SnapshotAt })
            .IsDescending(false, true)
            .HasDatabaseName("ix_obrigatoriedade_legal_historico_regra_snapshot_at");
    }
}
