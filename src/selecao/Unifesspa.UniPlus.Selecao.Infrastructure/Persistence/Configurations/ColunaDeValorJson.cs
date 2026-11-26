namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using System.Linq.Expressions;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Converters;

/// <summary>
/// A coluna <c>jsonb</c> obrigatória que guarda um valor JSON livre: grava o texto bruto do <see cref="JsonElement"/>
/// e o lê desanexado do <see cref="JsonDocument"/> de origem.
/// </summary>
internal static class ColunaDeValorJson
{
    private static readonly ValueComparer<JsonElement> Comparador = new(
        (a, b) => a.GetRawText() == b.GetRawText(),
        valor => valor.GetRawText().GetHashCode(StringComparison.Ordinal),
        valor => Desanexado(valor.GetRawText()));

    internal static JsonElement Desanexado(string json)
    {
        using JsonDocument documento = JsonDocument.Parse(json);
        return documento.RootElement.Clone();
    }

    internal static void MapearValorJson<TEntidade>(
        this EntityTypeBuilder<TEntidade> builder, Expression<Func<TEntidade, JsonElement>> propriedade, string nomeDoValor)
        where TEntidade : class =>
        builder.Property(propriedade)
            .HasConversion(Conversor(nomeDoValor), Comparador)
            .HasColumnType("jsonb")
            .IsRequired();

    private static ValueConverter<JsonElement, string> Conversor(string nomeDoValor) => new(
        valor => valor.GetRawText(),
        json => ObjetoDeValorGravado.DesserializarDocumento(() => Desanexado(json), nomeDoValor));
}
