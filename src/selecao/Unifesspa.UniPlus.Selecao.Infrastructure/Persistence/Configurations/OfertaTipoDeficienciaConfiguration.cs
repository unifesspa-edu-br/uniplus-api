namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

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

        // Teto do código no cadastro de origem (CodigoTipoDeficiencia). É por ele que
        // a regra legal referencia o tipo — o nome fica como rótulo de exibição.
        builder.Property(t => t.TipoDeficienciaCodigo).HasMaxLength(50).IsRequired();

        builder.Property(t => t.TipoDeficienciaNome).HasMaxLength(300).IsRequired();

        builder.HasIndex(t => new { t.OfertaAtendimentoEspecializadoId, t.TipoDeficienciaOrigemId }).IsUnique();
    }
}
