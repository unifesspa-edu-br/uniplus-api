namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// Configuração EF Core de <see cref="RegraDerivacaoConfigurada"/> (Story #927) — entidade filha de
/// <see cref="ConfiguracaoDerivacaoFato"/>, <c>EntityBase</c> puro.
/// </summary>
public sealed class RegraDerivacaoConfiguradaConfiguration : IEntityTypeConfiguration<RegraDerivacaoConfigurada>
{
    private const int ContribuiMaxLength = 60;

    public void Configure(EntityTypeBuilder<RegraDerivacaoConfigurada> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("regras_derivacao_configuradas");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.Ordem).IsRequired();
        builder.Property(r => r.Contribui).HasMaxLength(ContribuiMaxLength).IsRequired();

        // A ordem é total e única dentro da configuração — invariante do agregado, garantida também
        // pelo índice para a serialização determinística da regra.
        builder.HasIndex(r => new { r.ConfiguracaoDerivacaoFatoId, r.Ordem })
            .IsUnique()
            .HasDatabaseName("ux_regras_derivacao_configuradas_config_ordem");

        builder.HasMany(r => r.Condicoes)
            .WithOne()
            .HasForeignKey(c => c.RegraDerivacaoConfiguradaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(r => r.Condicoes)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
