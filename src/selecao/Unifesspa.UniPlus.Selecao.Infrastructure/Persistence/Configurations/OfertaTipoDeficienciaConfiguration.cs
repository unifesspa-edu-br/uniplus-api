namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Domain.Entities;

public sealed class OfertaTipoDeficienciaConfiguration : IEntityTypeConfiguration<OfertaTipoDeficiencia>
{
    public void Configure(EntityTypeBuilder<OfertaTipoDeficiencia> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ofertas_tipo_deficiencia");
        builder.HasKey(t => t.Id);
        // Chave Guid v7 do domínio (EntityBase) — ValueGeneratedNever para o EF
        // tratar a chave como fornecida pela aplicação (evita UPDATE de filho novo
        // ao reconfigurar o agregado tracked). Convenção do repo.
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.TipoDeficienciaOrigemId).IsRequired();
        builder.Property(t => t.TipoDeficienciaNome).HasMaxLength(300).IsRequired();

        builder.HasIndex(t => new { t.OfertaAtendimentoEspecializadoId, t.TipoDeficienciaOrigemId }).IsUnique();
    }
}
