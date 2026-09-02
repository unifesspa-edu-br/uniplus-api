namespace Unifesspa.UniPlus.Discentes.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Discentes.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Entities;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class SyncRunConfiguration : IEntityTypeConfiguration<SyncRun>
{
    public void Configure(EntityTypeBuilder<SyncRun> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("sync_run", t => t.HasComment(
            "Controle de execução das rotinas de sincronização de dados discentes com o SIGAA — uma linha por execução."));

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasComment("Identificador interno (UUIDv7) da execução.");

        builder.Property(s => s.Status)
            .HasComment("Estado da execução (Running/Completed/Partial/Failed).");

        builder.Property(s => s.DataDeReferencia)
            .HasComment(
                "Dia a que a execução se refere — não o instante em que rodou. Uma execução "
                + "disparada de madrugada refere-se ao dia que começou.");

        builder.Property(s => s.TotalItems)
            .HasComment("Quantidade total de itens previstos para esta execução.");

        builder.Property(s => s.ProcessedItems)
            .HasComment("Quantidade de itens já processados.");

        builder.Property(s => s.SuccessCount)
            .HasComment("Quantidade de itens processados com sucesso.");

        builder.Property(s => s.ErrorCount)
            .HasComment("Quantidade de itens que falharam durante o processamento.");

        builder.Property(s => s.StartedAt)
            .HasComment("Instante de início da execução.");

        builder.Property(s => s.FinishedAt)
            .HasComment("Instante de conclusão da execução — nulo enquanto o status é Running.");

        builder.Property(s => s.CreatedAt)
            .HasComment("Instante de criação do registro (auditoria, carimbado pelo AuditableInterceptor).");

        builder.Property(s => s.UpdatedAt)
            .HasComment("Instante da última atualização do registro (auditoria, carimbado pelo AuditableInterceptor).");

        builder.HasIndex(s => s.StartedAt)
            .HasDatabaseName("ix_sync_run_started_at");

        builder.HasIndex(s => s.DataDeReferencia)
            .HasDatabaseName("ix_sync_run_data_de_referencia");
    }
}
