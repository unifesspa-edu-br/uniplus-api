namespace Unifesspa.UniPlus.Selecao.Application.Services.ExportacaoDaVersaoPublicada;

using System.Text.Json.Nodes;

internal static class ProjecaoDePublicacoesPrevistas
{
    public static void Preparar(JsonObject origem, JsonObject congelado)
    {
        if (origem["publicacoesPrevistas"] is JsonArray publicacoes)
        {
            origem["publicacoesPrevistas"] = new JsonArray([.. publicacoes.OfType<JsonObject>()
                .Select(publicacao => PrepararPublicacao(publicacao, congelado))]);
        }
    }

    private static JsonObject PrepararPublicacao(JsonObject publicacao, JsonObject congelado)
    {
        JsonObject copia = (JsonObject)publicacao.DeepClone();
        if (publicacao["atoCodigo"] is { } codigo)
        {
            JsonObject? ato = (congelado["tiposDeAto"] as JsonArray)?.OfType<JsonObject>()
                .FirstOrDefault(item => string.Equals(item["codigo"]?.ToString(), codigo.ToString(), StringComparison.Ordinal));
            if (ato is null)
                throw new InvalidOperationException($"Tipo de ato '{codigo}' não encontrado no documento congelado.");
            ProjecaoDeParte.Transferir(copia, "atoNome", ato["nome"]);
        }
        return copia;
    }
}
