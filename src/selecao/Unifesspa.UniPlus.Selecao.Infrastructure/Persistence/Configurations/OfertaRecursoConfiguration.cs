namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Domain.Entities;

public sealed class OfertaRecursoConfiguration : IEntityTypeConfiguration<OfertaRecurso>
{
    public void Configure(EntityTypeBuilder<OfertaRecurso> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ofertas_recurso");
        builder.HasKey(r => r.Id);
        // Chave Guid v7 do domínio (EntityBase) — ValueGeneratedNever para o EF
        // tratar a chave como fornecida pela aplicação (evita UPDATE de filho novo
        // ao reconfigurar o agregado tracked). Convenção do repo.
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.RecursoOrigemId).IsRequired();
        builder.Property(r => r.RecursoNome).HasMaxLength(300).IsRequired();

        builder.HasIndex(r => new { r.OfertaAtendimentoEspecializadoId, r.RecursoOrigemId }).IsUnique();
    }
}
