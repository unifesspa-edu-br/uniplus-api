namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using System.Text.Json;

using Domain.Entities;
using Domain.Enums;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

/// <summary>
/// Configuração EF Core de <see cref="CondicaoRegraDerivacao"/> (Story #927) — entidade filha de
/// <see cref="RegraDerivacaoConfigurada"/>, <c>EntityBase</c> puro (sem soft-delete). Mesmo mapeamento de
/// <see cref="CondicaoGatilhoConfiguration"/>: o operador vai para a coluna pelo token canônico do
/// wire, nunca por <c>enum.ToString()</c>, e o valor é <c>jsonb</c> serializado pelo texto bruto.
/// </summary>
public sealed class CondicaoRegraDerivacaoConfiguration : IEntityTypeConfiguration<CondicaoRegraDerivacao>
{
    private const int FatoMaxLength = 60;
    private const int OperadorCodigoMaxLength = 20;

    public void Configure(EntityTypeBuilder<CondicaoRegraDerivacao> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("condicoes_regra_derivacao");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.Clausula).IsRequired();
        builder.Property(c => c.Fato).HasMaxLength(FatoMaxLength).IsRequired();

        builder.Property(c => c.Operador)
            .HasConversion(OperadorConverter)
            .HasMaxLength(OperadorCodigoMaxLength)
            .IsRequired();

        builder.Property(c => c.Valor)
            .HasConversion(JsonElementConverter, JsonElementComparer)
            .HasColumnType("jsonb")
            .IsRequired();
    }

    private static readonly ValueConverter<Operador, string> OperadorConverter =
        new(operador => operador.ToCodigo(), codigo => OperadorCodigo.FromCodigo(codigo));

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
