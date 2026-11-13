namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>Configuração EF Core de <see cref="BancaRequerida"/> (Story #851) — filha 0..* de <see cref="FaseCronograma"/>.</summary>
public sealed class BancaRequeridaConfiguration : IEntityTypeConfiguration<BancaRequerida>
{
    private const int CodigoMaxLength = 60;
    private const int NomeMaxLength = 200;

    public void Configure(EntityTypeBuilder<BancaRequerida> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.ToTable("bancas_requeridas");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();

        builder.Property(b => b.TipoBancaOrigemId).IsRequired();
        builder.Property(b => b.Codigo).HasMaxLength(CodigoMaxLength).IsRequired();
        builder.Property(b => b.Nome).HasMaxLength(NomeMaxLength).IsRequired();
        builder.OwnsOne(b => b.RegraDeDeliberacao, regra => regra.ConfigurarRegraDeDeliberacao());

        builder.HasMany(b => b.RecorteDeCompetencia)
            .WithOne()
            .HasForeignKey(c => c.BancaRequeridaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(b => b.RecorteDeCompetencia)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
