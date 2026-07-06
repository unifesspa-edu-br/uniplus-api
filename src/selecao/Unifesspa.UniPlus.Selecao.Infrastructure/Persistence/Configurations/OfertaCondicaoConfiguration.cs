namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Domain.Entities;

public sealed class OfertaCondicaoConfiguration : IEntityTypeConfiguration<OfertaCondicao>
{
    public void Configure(EntityTypeBuilder<OfertaCondicao> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ofertas_condicao");
        builder.HasKey(c => c.Id);
        // Chave Guid v7 do domínio (EntityBase) — ValueGeneratedNever para o EF
        // tratar a chave como fornecida pela aplicação (evita UPDATE de filho novo
        // ao reconfigurar o agregado tracked). Convenção do repo.
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.CondicaoOrigemId).IsRequired();
        builder.Property(c => c.CondicaoCodigo).HasMaxLength(50).IsRequired();
        builder.Property(c => c.CondicaoNome).HasMaxLength(300).IsRequired();

        builder.HasIndex(c => new { c.OfertaAtendimentoEspecializadoId, c.CondicaoOrigemId }).IsUnique();
    }
}
