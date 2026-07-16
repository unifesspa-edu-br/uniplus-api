namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Domain.Entities;

/// <summary>Configuração EF Core de <see cref="BancaRequerida"/> (Story #851) — filha 0..* de <see cref="FaseCronograma"/>.</summary>
public sealed class BancaRequeridaConfiguration : IEntityTypeConfiguration<BancaRequerida>
{
    private const int CodigoMaxLength = 60;

    public void Configure(EntityTypeBuilder<BancaRequerida> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("bancas_requeridas");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();

        builder.Property(b => b.TipoBancaOrigemId).IsRequired();
        builder.Property(b => b.Codigo).HasMaxLength(CodigoMaxLength).IsRequired();
    }
}
