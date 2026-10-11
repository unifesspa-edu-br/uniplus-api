namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;

using System.Text.Json;

using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// As regras padrão de um derivado numa coluna <c>jsonb</c>: cada regra com o que contribui (nulo
/// no booleano) e o predicado na forma de <see cref="PredicadoDnfGravado"/>.
/// </summary>
internal static class RegrasPadraoJson
{
    private static readonly JsonSerializerOptions Opcoes = new(JsonSerializerDefaults.Web);

    public static readonly ValueConverter<IReadOnlyList<RegraDerivacao>, string> Converter =
        new(regras => Serializar(regras), json => Desserializar(json));

    public static readonly ValueComparer<IReadOnlyList<RegraDerivacao>> Comparer =
        new(
            (a, b) => Serializar(a!) == Serializar(b!),
            regras => Serializar(regras).GetHashCode(StringComparison.Ordinal),
            regras => Desserializar(Serializar(regras)));

    public static string Serializar(IReadOnlyList<RegraDerivacao> regras) =>
        JsonSerializer.Serialize(
            regras.Select(static r => new RegraGravada(r.Contribui, PredicadoDnfGravado.Gravar(r.Quando))),
            Opcoes);

    public static IReadOnlyList<RegraDerivacao> Desserializar(string json)
    {
        IReadOnlyList<RegraGravada> gravadas = JsonSerializer.Deserialize<IReadOnlyList<RegraGravada>>(json, Opcoes) ?? [];
        return [.. gravadas.Select(Remontar)];
    }

    private static RegraDerivacao Remontar(RegraGravada gravada)
    {
        PredicadoDnf quando = PredicadoDnfGravado.Remontar(gravada.Quando);
        return gravada.Contribui is null
            ? RegraDerivacao.CriarBooleana(quando)
            : PredicadoDnfGravado.Exigir(RegraDerivacao.Criar(quando, gravada.Contribui));
    }

    private sealed record RegraGravada(string? Contribui, IReadOnlyList<IReadOnlyList<PredicadoDnfGravado.CondicaoGravada>> Quando);
}
