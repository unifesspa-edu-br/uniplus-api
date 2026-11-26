namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// Configuração EF Core de <see cref="ConfiguracaoCascataRemanejamento"/>
/// (Story #575) — entidade 0..1 do agregado <see cref="ProcessoSeletivo"/>.
/// </summary>
internal sealed class ConfiguracaoCascataRemanejamentoConfiguration : IEntityTypeConfiguration<ConfiguracaoCascataRemanejamento>
{

    public void Configure(EntityTypeBuilder<ConfiguracaoCascataRemanejamento> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.ToTable("configuracoes_de_remanejamento", tabela => tabela.HasComment("Regra e destino final do remanejamento de vagas do processo."));
        builder.HasKey(c => c.Id);
        builder.Property(c => c.ProcessoSeletivoId).HasComment("Identificador do processo seletivo ao qual a configuração pertence.");
        builder.Property(c => c.Id).HasComment("Identificador da configuração de remanejamento.").ValueGeneratedNever();

        builder.OwnsOne(c => c.Regra, regra =>
        {
            regra.ConfigurarReferenciaRegra("regra");
            regra.Property(x => x.Codigo).HasComment("Código da regra de remanejamento.");
            regra.Property(x => x.Versao).HasComment("Versão da regra de remanejamento.");
            regra.Property(x => x.Hash).HasComment("Resumo do conteúdo da regra de remanejamento.");
        });
        builder.Navigation(c => c.Regra).IsRequired();

        builder.Property(c => c.FallbackCodigo).HasComment("Código da modalidade de destino final das vagas.").HasColumnName("destino_final_codigo").HasMaxLength(ConfiguracaoCascataRemanejamento.FallbackMaxLength).IsRequired();

        // Coleção filha: entidade própria com FK para a raiz (nunca owned types) —
        // mesmo padrão de ConfiguracaoDistribuicaoVagas.Modalidades.
        builder.HasMany(c => c.Destinos)
            .WithOne()
            .HasForeignKey(d => d.ConfiguracaoCascataRemanejamentoId)
            .HasConstraintName("fk_destinos_remanejamento_configuracao_de_remanejamento")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(c => c.Destinos)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasOne<ProcessoSeletivo>()
            .WithOne(p => p.Cascata)
            .HasForeignKey<ConfiguracaoCascataRemanejamento>(c => c.ProcessoSeletivoId)
            .HasConstraintName("fk_configuracoes_de_remanejamento_processo_seletivo")
            .OnDelete(DeleteBehavior.Cascade);

        // Uma cascata por processo (§2.2 da story — a cascata NÃO é por oferta).
        builder.HasIndex(c => c.ProcessoSeletivoId)
            .IsUnique()
            .HasDatabaseName("ux_configuracoes_de_remanejamento_processo");
    }
}
