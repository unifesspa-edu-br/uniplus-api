namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

public sealed class AtendimentoEspecializadoConfiguration : IEntityTypeConfiguration<AtendimentoEspecializado>
{
    public void Configure(EntityTypeBuilder<AtendimentoEspecializado> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.ToTable("atendimentos_especializados");
        builder.HasKey(o => o.Id);
        // Chave Guid v7 do domínio (EntityBase) — ValueGeneratedNever para o EF
        // tratar a chave como fornecida pela aplicação (evita UPDATE de filho novo
        // ao reconfigurar o agregado tracked). Convenção do repo.
        builder.Property(o => o.Id).ValueGeneratedNever();

        // Um contêiner de atendimento por processo.
        builder.HasIndex(o => o.ProcessoSeletivoId).IsUnique()
            .HasDatabaseName("ux_atendimentos_especializados_processo_seletivo_id");

        builder.HasMany(o => o.Condicoes)
            .WithOne()
            .HasForeignKey(c => c.AtendimentoEspecializadoId)
            .HasConstraintName("fk_condicoes_ofertadas_atendimento_especializado")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(o => o.Recursos)
            .WithOne()
            .HasForeignKey(r => r.AtendimentoEspecializadoId)
            .HasConstraintName("fk_recursos_ofertados_atendimento_especializado")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(o => o.TiposDeficiencia)
            .WithOne()
            .HasForeignKey(t => t.AtendimentoEspecializadoId)
            .HasConstraintName("fk_deficiencias_atendidas_atendimento_especializado")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(o => o.Condicoes)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(o => o.Recursos)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(o => o.TiposDeficiencia)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
