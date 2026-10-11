namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;

using System.Text.Json;

using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>Um predicado numa coluna <c>jsonb</c>, na forma de <see cref="PredicadoDnfGravado"/>.</summary>
internal static class PredicadoDnfColunaJson
{
    private static readonly JsonSerializerOptions Opcoes = new(JsonSerializerDefaults.Web);

    public static readonly ValueConverter<PredicadoDnf, string> Converter =
        new(predicado => Serializar(predicado), json => Desserializar(json));

    public static readonly ValueComparer<PredicadoDnf> Comparer =
        new(
            (a, b) => Serializar(a!) == Serializar(b!),
            predicado => Serializar(predicado).GetHashCode(StringComparison.Ordinal),
            predicado => Desserializar(Serializar(predicado)));

    public static string Serializar(PredicadoDnf predicado) =>
        JsonSerializer.Serialize(PredicadoDnfGravado.Gravar(predicado), Opcoes);

    public static PredicadoDnf Desserializar(string json) =>
        PredicadoDnfGravado.Remontar(
            JsonSerializer.Deserialize<IReadOnlyList<IReadOnlyList<PredicadoDnfGravado.CondicaoGravada>>>(json, Opcoes) ?? []);
}
