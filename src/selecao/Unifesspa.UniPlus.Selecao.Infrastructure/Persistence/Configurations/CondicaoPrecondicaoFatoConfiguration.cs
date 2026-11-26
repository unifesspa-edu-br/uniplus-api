namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Domain.Entities;
using Domain.Enums;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// Configuração EF Core de <see cref="CondicaoPrecondicaoFato"/> — entidade filha de
/// <see cref="FatoColetado"/>, <c>EntityBase</c> puro (sem soft-delete). Mesmo mapeamento de
/// <see cref="CondicaoGatilhoConfiguration"/>: o operador vai para a coluna pelo código declarado no enum
/// e o valor é <c>jsonb</c> serializado pelo texto bruto.
/// </summary>
public sealed class CondicaoPrecondicaoFatoConfiguration : IEntityTypeConfiguration<CondicaoPrecondicaoFato>
{
    private const int FatoMaxLength = FatoDoCatalogo.CodigoMaxLength;
    private const int OperadorCodigoMaxLength = 20;

    public void Configure(EntityTypeBuilder<CondicaoPrecondicaoFato> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.Property(c => c.FatoColetadoId).HasColumnName("dado_do_formulario_id").HasComment("Identificador do dado do formulário ao qual a condição pertence.");
        builder.Property(c => c.Clausula).HasComment("Número da alternativa que reúne condições atendidas em conjunto.");
        builder.Property(c => c.Fato).HasComment("Código do dado do candidato usado na condição. Exemplos: RENDA_PER_CAPITA, CONCORRER_RENDA.");
        builder.Property(c => c.Operador).HasComment("Comparação aplicada ao valor.");
        builder.Property(c => c.Valor).HasComment("Valor de comparação da condição, em JSON.");

        builder.ToTable("condicoes_de_exibicao_do_dado", t => t.HasComment("Condições que determinam a exibição de um dado no formulário."));
        builder.HasKey(c => c.Id);
        builder.HasIndex(c => c.FatoColetadoId).HasDatabaseName("ix_condicoes_de_exibicao_do_dado_dado_do_formulario_id");
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.Clausula).HasColumnName("alternativa").IsRequired();
        builder.Property(c => c.Fato).HasColumnName("codigo_do_dado").HasMaxLength(FatoMaxLength).IsRequired();

        builder.Property(c => c.Operador)
            .HasMaxLength(OperadorCodigoMaxLength)
            .IsRequired();

        builder.MapearValorJson(c => c.Valor, nameof(CondicaoPrecondicaoFato.Valor));
    }
}
