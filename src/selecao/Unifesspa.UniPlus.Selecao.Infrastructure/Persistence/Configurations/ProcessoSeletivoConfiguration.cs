namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Domain.Entities;

public sealed class ProcessoSeletivoConfiguration : IEntityTypeConfiguration<ProcessoSeletivo>
{
    public void Configure(EntityTypeBuilder<ProcessoSeletivo> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("processos_seletivos");
        builder.HasKey(p => p.Id);
        // Chave Guid v7 gerada no domínio (EntityBase): ValueGeneratedNever
        // força o EF a tratar a chave como fornecida pela aplicação. Sem isso,
        // ao reconfigurar o agregado tracked (substituir filhos com Guid v7 já
        // preenchido), o EF marcaria os filhos novos como Modified → UPDATE de
        // linhas nunca inseridas. Convenção do repo (ver UnidadeIdentificadorHistorico).
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.Nome).HasMaxLength(300).IsRequired();
        builder.Property(p => p.Tipo).HasConversion<int>().IsRequired();
        builder.Property(p => p.Status).HasConversion<int>().IsRequired();

        // Coleções filhas do agregado: entidades próprias com FK para a raiz
        // (nunca owned types).
        builder.HasMany(p => p.Etapas)
            .WithOne()
            .HasForeignKey(e => e.ProcessoSeletivoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.OfertaAtendimento)
            .WithOne()
            .HasForeignKey<OfertaAtendimentoEspecializado>(o => o.ProcessoSeletivoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.DistribuicaoVagas)
            .WithOne()
            .HasForeignKey(d => d.ProcessoSeletivoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.BonusRegional)
            .WithOne()
            .HasForeignKey<ConfiguracaoBonusRegional>(b => b.ProcessoSeletivoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.CriteriosDesempate)
            .WithOne()
            .HasForeignKey(c => c.ProcessoSeletivoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.Classificacao)
            .WithOne()
            .HasForeignKey<ConfiguracaoClassificacao>(c => c.ProcessoSeletivoId)
            .OnDelete(DeleteBehavior.Cascade);

        // A sessão editorial (ADR-0110 D3) — 1:1, como as demais filhas singulares. Ela é
        // efêmera (apagada no fechamento e no descarte) e não é evidência forense: a
        // auditoria com peso jurídico vive na VersaoConfiguracao, que é append-only.
        builder.HasOne(p => p.Rascunho)
            .WithOne()
            .HasForeignKey<RascunhoRetificacao>(r => r.ProcessoSeletivoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.Etapas)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Navigation(p => p.DistribuicaoVagas)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Navigation(p => p.CriteriosDesempate)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
