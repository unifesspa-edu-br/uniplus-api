namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// Configuração EF Core de <see cref="ConfiguracaoBonusRegional"/> (Story
/// #774, RN05) — entidade 0..1 do agregado <see cref="ProcessoSeletivo"/>.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class ConfiguracaoBonusRegionalConfiguration : IEntityTypeConfiguration<ConfiguracaoBonusRegional>
{
    private const int RegraCodigoMaxLength = 128;
    private const int RegraVersaoMaxLength = 16;
    private const int HashLength = 64;
    private const int TipoInstrumentoMaxLength = 30;
    private const int IdentificacaoMaxLength = 500;
    private const int DescricaoMaxLength = 2000;

    public void Configure(EntityTypeBuilder<ConfiguracaoBonusRegional> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("configuracoes_bonus_regional");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();

        builder.OwnsOne(b => b.Regra, regra =>
        {
            regra.Property(r => r.Codigo).HasColumnName("regra_codigo").HasMaxLength(RegraCodigoMaxLength).IsRequired();
            regra.Property(r => r.Versao).HasColumnName("regra_versao").HasMaxLength(RegraVersaoMaxLength).IsRequired();
            regra.Property(r => r.Hash).HasColumnName("regra_hash").HasMaxLength(HashLength).IsFixedLength().IsRequired();
        });
        builder.Navigation(b => b.Regra).IsRequired();

        builder.Property(b => b.Fator).HasPrecision(6, 4).IsRequired();
        builder.Property(b => b.Teto).HasPrecision(6, 4);

        builder.Property(b => b.BaseLegalBonusRegionalId).IsRequired();
        builder.Property(b => b.TipoInstrumento).HasMaxLength(TipoInstrumentoMaxLength).IsRequired();
        builder.Property(b => b.Identificacao).HasMaxLength(IdentificacaoMaxLength).IsRequired();
        builder.Property(b => b.Descricao).HasMaxLength(DescricaoMaxLength).IsRequired();

        builder.HasMany(b => b.Municipios)
            .WithOne()
            .HasForeignKey(m => m.ConfiguracaoBonusRegionalId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(b => b.Municipios)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
