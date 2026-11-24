namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using System.Text.Json;

using Domain.Entities;
using Domain.Enums;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// Configuração EF Core de <see cref="CondicaoRegraDerivacao"/> (Story #927) — entidade filha de
/// <see cref="RegraDerivacaoConfigurada"/>, <c>EntityBase</c> puro (sem soft-delete). Mesmo mapeamento de
/// <see cref="CondicaoGatilhoConfiguration"/>: o operador vai para a coluna pelo código declarado no enum
/// e o valor é <c>jsonb</c> serializado pelo texto bruto.
/// </summary>
public sealed class CondicaoRegraDerivacaoConfiguration : IEntityTypeConfiguration<CondicaoRegraDerivacao>
{
    private const int FatoMaxLength = 60;
    private const int OperadorCodigoMaxLength = 20;

    public void Configure(EntityTypeBuilder<CondicaoRegraDerivacao> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.Property(c => c.RegraDerivacaoConfiguradaId).HasComment("Identificador da regra de cálculo à qual a condição pertence.");
        builder.Property(c => c.Clausula).HasComment("Número da alternativa que reúne condições atendidas em conjunto.");
        builder.Property(c => c.Fato).HasComment("Código do dado do candidato usado na condição. Exemplos: RENDA_PER_CAPITA, CONCORRER_RENDA.");
        builder.Property(c => c.Operador).HasComment("Comparação aplicada ao valor: ALGUM_DE, DIFERENTE, IGUAL, MAIOR_IGUAL, MENOR_IGUAL ou NENHUM_DE.");
        builder.Property(c => c.Valor).HasComment("Valor de comparação da condição, em JSON.");

        builder.ToTable("condicoes_do_calculo_dos_dados", t => t.HasComment("Condições que determinam a aplicação de uma regra de cálculo."));
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.HasIndex(c => c.RegraDerivacaoConfiguradaId)
            .HasDatabaseName("ix_condicoes_do_calculo_dos_dados_regra_id");

        builder.Property(c => c.Clausula).HasColumnName("alternativa").IsRequired();
        builder.Property(c => c.Fato).HasColumnName("codigo_do_dado").HasMaxLength(FatoMaxLength).IsRequired();

        builder.Property(c => c.Operador)
            .HasMaxLength(OperadorCodigoMaxLength)
            .IsRequired();

        builder.Property(c => c.Valor)
            .HasConversion(JsonElementConverter, JsonElementComparer)
            .HasColumnType("jsonb")
            .IsRequired();
    }

    private static readonly ValueConverter<JsonElement, string> JsonElementConverter =
        new(element => element.GetRawText(), json => Parse(json));

    private static readonly ValueComparer<JsonElement> JsonElementComparer =
        new(
            (a, b) => a.GetRawText() == b.GetRawText(),
            v => v.GetRawText().GetHashCode(StringComparison.Ordinal),
            v => Parse(v.GetRawText()));

    private static JsonElement Parse(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
