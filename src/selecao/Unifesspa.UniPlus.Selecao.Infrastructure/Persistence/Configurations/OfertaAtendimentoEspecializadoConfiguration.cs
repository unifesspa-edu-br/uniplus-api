namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class OfertaAtendimentoEspecializadoConfiguration : IEntityTypeConfiguration<OfertaAtendimentoEspecializado>
{
    public void Configure(EntityTypeBuilder<OfertaAtendimentoEspecializado> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ofertas_atendimento_especializado");
        builder.HasKey(o => o.Id);
        // Chave Guid v7 do domínio (EntityBase) — ValueGeneratedNever para o EF
        // tratar a chave como fornecida pela aplicação (evita UPDATE de filho novo
        // ao reconfigurar o agregado tracked). Convenção do repo.
        builder.Property(o => o.Id).ValueGeneratedNever();

        // Um contêiner de atendimento por processo.
        builder.HasIndex(o => o.ProcessoSeletivoId).IsUnique();

        builder.HasMany(o => o.Condicoes)
            .WithOne()
            .HasForeignKey(c => c.OfertaAtendimentoEspecializadoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(o => o.Recursos)
            .WithOne()
            .HasForeignKey(r => r.OfertaAtendimentoEspecializadoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(o => o.TiposDeficiencia)
            .WithOne()
            .HasForeignKey(t => t.OfertaAtendimentoEspecializadoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(o => o.Condicoes)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(o => o.Recursos)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(o => o.TiposDeficiencia)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
