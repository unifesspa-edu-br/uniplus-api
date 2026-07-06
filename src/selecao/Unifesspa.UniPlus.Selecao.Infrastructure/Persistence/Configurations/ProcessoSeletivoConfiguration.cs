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
        // (nunca owned types). Bônus/desempate/classificação entram nas fatias
        // F3–F4 sobre o rol_de_regras.
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

        builder.Navigation(p => p.Etapas)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Navigation(p => p.DistribuicaoVagas)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
