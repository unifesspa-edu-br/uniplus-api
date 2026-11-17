namespace Unifesspa.UniPlus.Selecao.Infrastructure.DocumentoDaConfiguracao;

using System.Text.Json.Nodes;

/// <summary>Diz em português que tipo de valor um trecho do esquema espera, a partir da palavra <c>type</c> dele.</summary>
internal static class TiposDoEsquema
{
    private static readonly Dictionary<string, string> NomesDosTipos = new(StringComparer.Ordinal)
    {
        ["string"] = "texto",
        ["integer"] = "número inteiro",
        ["number"] = "número",
        ["boolean"] = "verdadeiro ou falso",
        ["array"] = "lista",
        ["object"] = "objeto",
        ["null"] = "nulo",
    };

    public static string Esperado(TrechoDoEsquema trecho) =>
        string.Join(" ou ", Declarados(trecho).Select(static tipo => NomesDosTipos.GetValueOrDefault(tipo, tipo)).DefaultIfEmpty("valor válido"));

    private static IEnumerable<string> Declarados(TrechoDoEsquema trecho) => trecho.PalavraChave("type") switch
    {
        JsonValue valor when valor.TryGetValue(out string? tipo) => [tipo],
        JsonArray lista => lista.OfType<JsonValue>().Select(static valor => valor.GetValue<string>()),
        _ => [],
    };
}
