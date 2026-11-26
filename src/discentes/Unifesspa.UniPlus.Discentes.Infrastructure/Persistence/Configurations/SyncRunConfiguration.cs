namespace Unifesspa.UniPlus.Discentes.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Discentes.Domain.Entities;
using Unifesspa.UniPlus.Discentes.Domain.Enums;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.CodigosDeContrato;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

internal sealed class SyncRunConfiguration : IEntityTypeConfiguration<SyncRun>
{
    private const int CodigoDeContratoMaxLength = 30;

    public void Configure(EntityTypeBuilder<SyncRun> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.ToTable("sincronizacoes_com_o_sigaa", t =>
        {
            t.HasComment(
                "Controle de execução das rotinas de sincronização de dados discentes com o SIGAA — uma linha por execução.");
            t.HasCheckConstraint(
                "ck_sincronizacoes_com_o_sigaa_status",
                $"status IN ({SqlDosCodigosDeContrato.Lista<SyncRunStatus>()})");
        });

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasComment("Identificador interno (UUIDv7) da execução.");

        builder.Property(s => s.Status)
            .HasMaxLength(CodigoDeContratoMaxLength)
            .HasComment("Situação da execução.");

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
            .HasComment("Instante de conclusão da execução — nulo enquanto o status é EM_ANDAMENTO.");



        builder.HasIndex(s => s.StartedAt)
            .HasDatabaseName("ix_sincronizacoes_com_o_sigaa_started_at");

        builder.HasIndex(s => s.DataDeReferencia)
            .HasDatabaseName("ix_sincronizacoes_com_o_sigaa_data_de_referencia");
    }
}
