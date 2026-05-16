namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// Configuração EF Core da tabela append-only
/// <c>edital_governance_snapshot</c> (CA-04 da Story #460). Schema apenas —
/// a inserção pelo agregado <c>Edital.Publicar()</c> entra em #462
/// (US-F4-04). Em V1 a tabela existe vazia.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class EditalGovernanceSnapshotConfiguration : IEntityTypeConfiguration<EditalGovernanceSnapshot>
{
    public void Configure(EntityTypeBuilder<EditalGovernanceSnapshot> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("edital_governance_snapshot");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.EditalId).IsRequired();

        builder.Property(s => s.RegrasJson)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(s => s.SnapshottedAt).IsRequired();

        // FK para editais (edital alvo do snapshot). Mesmo padrão de
        // obrigatoriedade_legal_historico (ADR-0063): sem nav property
        // para evitar carregar Edital acidentalmente em queries forense;
        // ON DELETE RESTRICT bloqueia hard-delete do Edital mãe — soft-
        // delete (Modified + IsDeleted=true) não dispara. Tabela está
        // vazia em V1, mas a constraint é defensiva para o INSERT path
        // que entra em #462 (US-F4-04).
        builder.HasOne<Edital>()
            .WithMany()
            .HasForeignKey(s => s.EditalId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_edital_governance_snapshot_edital_id");

        // Consulta "snapshot do edital" é o único caso de uso previsto até
        // #462 ativar a leitura — index simples por edital_id basta. Também
        // serve o lookup do FK (edital_id como leading column).
        builder.HasIndex(s => s.EditalId)
            .HasDatabaseName("ix_edital_governance_snapshot_edital_id");
    }
}
