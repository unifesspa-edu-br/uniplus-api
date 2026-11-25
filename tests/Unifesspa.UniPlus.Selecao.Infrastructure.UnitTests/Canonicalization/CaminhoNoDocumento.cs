namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

internal static class CaminhoNoDocumento
{
    internal static bool Existe(JsonNode documento, string caminho)
    {
        JsonNode? atual = documento;
        foreach (Match parte in Regex.Matches(caminho, @"[^.\[\]]+|\[\d+\]"))
        {
            if (!Avancar(atual, parte.Value, out atual))
            {
                return false;
            }
        }

        return true;
    }

    private static bool Avancar(JsonNode? atual, string parte, out JsonNode? proximo)
    {
        if (atual is JsonObject objeto && objeto.TryGetPropertyValue(parte, out proximo))
        {
            return true;
        }

        if (atual is JsonArray array && parte.StartsWith('[')
            && int.TryParse(parte.AsSpan(1, parte.Length - 2), out int indice) && indice < array.Count)
        {
            proximo = array[indice];
            return true;
        }

        proximo = null;
        return false;
    }
}
