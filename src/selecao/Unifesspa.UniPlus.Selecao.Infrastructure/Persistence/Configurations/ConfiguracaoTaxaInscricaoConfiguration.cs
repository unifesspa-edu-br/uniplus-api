namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Configuração EF Core de <see cref="ConfiguracaoTaxaInscricao"/> (issue #1112) — entidade 1:1
/// do agregado <see cref="ProcessoSeletivo"/>.
/// </summary>
internal sealed class ConfiguracaoTaxaInscricaoConfiguration : IEntityTypeConfiguration<ConfiguracaoTaxaInscricao>
{
    public void Configure(EntityTypeBuilder<ConfiguracaoTaxaInscricao> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.ToTable("configuracoes_taxa_inscricao", t => t.HasComment(
            "Configuração de taxa de inscrição e fundamentos de isenção do processo seletivo " +
            "(issue #1112) — entidade dependente 1:1 de processos_seletivos."));
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .ValueGeneratedNever()
            .HasComment("Identificador interno (UUIDv7) — não confundir com o Id do processo seletivo, a FK.");

        builder.Property(c => c.ProcessoSeletivoId)
            .HasComment("Id do processo seletivo dono desta configuração (FK 1:1, cascade delete).");


        builder.Property(c => c.Cobra)
            .IsRequired()
            .HasComment("Declaração explícita de cobrança de taxa — nunca inferida pela ausência da linha (CA-01).");
        builder.Property(c => c.Valor)
            .HasPrecision(ConfiguracaoTaxaInscricao.ValorPrecisao, ConfiguracaoTaxaInscricao.ValorEscala)
            .HasComment("Valor da taxa em reais, positivo quando cobra=true; sempre nulo quando cobra=false (CA-02/CA-03).");

        builder.Ignore(c => c.Fundamentos);
        builder.PrimitiveCollection<List<FundamentoIsencao>>("_fundamentos")
            .HasColumnName("fundamentos")
            .HasComment("Fundamentos de isenção.")
            .IsRequired()
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
