namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// Configuração EF Core de <see cref="ConfiguracaoDerivacaoFato"/> (Story #927) — entidade filha de
/// <c>ProcessoSeletivo</c>, <c>EntityBase</c> puro. Substituível por inteiro junto com o processo,
/// mesmo padrão de <c>FatoColetado</c>.
/// </summary>
public sealed class ConfiguracaoDerivacaoFatoConfiguration : IEntityTypeConfiguration<ConfiguracaoDerivacaoFato>
{
    private const int FatoCodigoMaxLength = FatoDoCatalogo.CodigoMaxLength;

    public void Configure(EntityTypeBuilder<ConfiguracaoDerivacaoFato> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.Property(c => c.ProcessoSeletivoId).HasComment("Identificador do processo seletivo ao qual pertence.");
        builder.Property(c => c.CodigoFato).HasComment("Código do dado calculado. Exemplos: RENDA_PER_CAPITA, CONCORRER_RENDA.");

        builder.ToTable("calculos_dos_dados", t => t.HasComment("Configurações dos cálculos dos dados de candidatos em um processo."));
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.CodigoFato).HasColumnName("codigo_do_dado").HasMaxLength(FatoCodigoMaxLength).IsRequired();

        // Um fato derivado tem no máximo uma configuração de derivação por processo — invariante do
        // agregado, garantida também pelo índice contra qualquer caminho que não passe por ele.
        builder.HasIndex(c => new { c.ProcessoSeletivoId, c.CodigoFato })
            .IsUnique()
            .HasDatabaseName("ux_calculos_dos_dados_processo_dado");

        builder.HasMany(c => c.Regras)
            .WithOne()
            .HasForeignKey(r => r.ConfiguracaoDerivacaoFatoId)
            .HasConstraintName("fk_regras_de_calculo_dos_dados_calculo_do_dado")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(c => c.Regras)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
