namespace Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

using System.Globalization;
using System.Text.Json.Nodes;

/// <summary>Monta e desmonta os segmentos de um JSON Pointer (RFC 6901).</summary>
public static class PonteiroJson
{
    public static string Acrescentar(string ponteiro, string segmento)
    {
        ArgumentNullException.ThrowIfNull(ponteiro);
        ArgumentNullException.ThrowIfNull(segmento);

        return $"{ponteiro}/{segmento.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal)}";
    }

    public static IReadOnlyList<string> Segmentos(string ponteiro)
    {
        ArgumentNullException.ThrowIfNull(ponteiro);

        return ponteiro.Length == 0
            ? []
            : [.. ponteiro[1..].Split('/').Select(static segmento => segmento.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal))];
    }

    /// <summary>O valor que o ponteiro designa no documento, ou <see langword="null"/> quando o documento não o contém.</summary>
    public static JsonNode? Resolver(JsonNode documento, string ponteiro)
    {
        ArgumentNullException.ThrowIfNull(documento);

        return Segmentos(ponteiro).Aggregate((JsonNode?)documento, Descer);
    }

    private static JsonNode? Descer(JsonNode? pai, string segmento) => pai switch
    {
        JsonObject objeto => objeto[segmento],
        JsonArray lista when int.TryParse(segmento, NumberStyles.None, CultureInfo.InvariantCulture, out int indice) && indice < lista.Count => lista[indice],
        _ => null,
    };
}
