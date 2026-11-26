namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// Configuração EF Core de <see cref="DestinoRemanejamento"/> (Story #575) —
/// entidade filha de <see cref="ConfiguracaoCascataRemanejamento"/>.
/// </summary>
internal sealed class DestinoRemanejamentoConfiguration : IEntityTypeConfiguration<DestinoRemanejamento>
{
    public void Configure(EntityTypeBuilder<DestinoRemanejamento> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.ToTable("destinos_remanejamento", t => t.HasComment("Destinos ordenados para o remanejamento de vagas entre modalidades."));
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever().HasComment("Identificador do destino de remanejamento.");

        builder.Property(d => d.ConfiguracaoCascataRemanejamentoId)
            .HasColumnName("configuracao_de_remanejamento_id").HasComment("Configuração de remanejamento à qual pertence o destino.");
        builder.Property(d => d.ModalidadeOrigemCodigo).HasMaxLength(DestinoRemanejamento.CodigoMaxLength).IsRequired().HasComment("Código da modalidade que cede as vagas.");
        builder.Property(d => d.Ordem).IsRequired().HasComment("Posição do destino na ordem de remanejamento da origem.");
        builder.Property(d => d.ModalidadeDestinoCodigo).HasMaxLength(DestinoRemanejamento.CodigoMaxLength).IsRequired().HasComment("Código da modalidade que recebe as vagas.");

        builder.HasIndex(d => new { d.ConfiguracaoCascataRemanejamentoId, d.ModalidadeOrigemCodigo, d.Ordem })
            .IsUnique()
            .HasDatabaseName("ux_destinos_remanejamento_configuracao_origem_ordem");

        builder.HasIndex(d => new { d.ConfiguracaoCascataRemanejamentoId, d.ModalidadeOrigemCodigo, d.ModalidadeDestinoCodigo })
            .IsUnique()
            .HasDatabaseName("ux_destinos_remanejamento_configuracao_origem_destino");

        builder.ToTable(t => t.HasCheckConstraint("ck_destinos_remanejamento_ordem_positiva", "ordem >= 1"));
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_destinos_remanejamento_origem_diferente_destino",
            "modalidade_origem_codigo <> modalidade_destino_codigo"));
    }
}
