namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

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

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.Property(r => r.ConfiguracaoDerivacaoFatoId).HasComment("Identificador do cálculo ao qual a regra pertence.");
        builder.Property(r => r.Ordem).HasComment("Posição de apresentação ou avaliação no conjunto.");
        builder.Property(r => r.Contribui).HasComment("Código do valor produzido quando a regra é atendida, quando houver.");

        builder.ToTable("regras_de_calculo_dos_dados", t => t.HasComment("Regras ordenadas que compõem o cálculo de um dado."));
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.ConfiguracaoDerivacaoFatoId).HasColumnName("calculo_do_dado_id");
        builder.Property(r => r.Ordem).IsRequired();
        builder.Property(r => r.Contribui).HasMaxLength(ContribuiMaxLength);

        // A ordem é total e única dentro da configuração — invariante do agregado, garantida também
        // pelo índice para a serialização determinística da regra.
        builder.HasIndex(r => new { r.ConfiguracaoDerivacaoFatoId, r.Ordem })
            .IsUnique()
            .HasDatabaseName("ux_regras_de_calculo_dos_dados_config_ordem");

        builder.HasMany(r => r.Condicoes)
            .WithOne()
            .HasForeignKey(c => c.RegraDerivacaoConfiguradaId)
            .HasConstraintName("fk_condicoes_calculo_regra_de_calculo")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(r => r.Condicoes)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
