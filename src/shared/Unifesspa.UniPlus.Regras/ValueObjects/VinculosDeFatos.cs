namespace Unifesspa.UniPlus.Regras.ValueObjects;

using System.Text.Json;

/// <summary>
/// Os fatos do catálogo que a configuração do processo usa — coletados, derivados por regra ou
/// citados por condição — e os valores que as condições citam ou que as regras de derivação
/// contribuem. É o que conta como vínculo
/// existente quando o catálogo desativa um fato ou um valor: desativar recusa só vínculo novo
/// (ADR-0136).
/// </summary>
public sealed record VinculosDeFatos(IReadOnlySet<string> Fatos, IReadOnlySet<(string Fato, string Valor)> Valores)
{
    public static VinculosDeFatos Unir(IEnumerable<VinculosDeFatos> partes)
    {
        ArgumentNullException.ThrowIfNull(partes);
        VinculosDeFatos[] todas = [.. partes];
        return new VinculosDeFatos(
            todas.SelectMany(static p => p.Fatos).ToHashSet(StringComparer.Ordinal),
            todas.SelectMany(static p => p.Valores).ToHashSet());
    }

    /// <summary>
    /// Os vínculos dos fatos dados, das condições e dos valores contribuídos: cada condição vincula
    /// o fato que cita e o valor (ou cada valor da lista) que compara; cada contribuição de regra de
    /// derivação vincula o valor que produz para o fato derivado.
    /// </summary>
    public static VinculosDeFatos De(
        IEnumerable<string> fatos,
        IEnumerable<(string Fato, JsonElement Valor)> condicoes,
        IEnumerable<(string Fato, string Valor)>? contribuicoes = null)
    {
        ArgumentNullException.ThrowIfNull(fatos);
        ArgumentNullException.ThrowIfNull(condicoes);

        HashSet<string> vinculados = new(fatos, StringComparer.Ordinal);
        HashSet<(string, string)> valores = [.. contribuicoes ?? []];
        foreach ((string fato, JsonElement valor) in condicoes)
        {
            vinculados.Add(fato);
            valores.UnionWith(ValoresTextuaisCitados(fato, valor));
        }

        return new VinculosDeFatos(vinculados, valores);
    }

    private static IEnumerable<(string, string)> ValoresTextuaisCitados(string fato, JsonElement valor)
    {
        IEnumerable<JsonElement> citados = valor.ValueKind == JsonValueKind.Array ? valor.EnumerateArray() : [valor];

        return citados.Where(static v => v.ValueKind == JsonValueKind.String).Select(v => (fato, v.GetString()!));
    }
}
