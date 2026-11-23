namespace Unifesspa.UniPlus.Selecao.Application.Services.ExportacaoDaVersaoPublicada;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

internal static class ProjecaoDeCascataRemanejamento
{
    public static JsonNode? Projetar(JsonObject congelado, FormaDoNo formaDaParte, ChavesDoDocumentoCongelado chaves)
    {
        if (congelado["cascataRemanejamento"] is not JsonObject cascata || cascata["presente"]?.GetValue<bool>() != true)
        {
            return null;
        }
        JsonObject origem = (JsonObject)cascata.DeepClone();
        origem["destinos"] = Destinos(cascata["ordens"] as JsonArray);
        return CopiaPeloEsquema.Copiar(origem, formaDaParte);
    }

    private static JsonArray Destinos(JsonArray? ordens)
    {
        JsonArray resultado = [];
        foreach (JsonObject ordem in (ordens ?? []).OfType<JsonObject>())
        {
            AdicionarDestinos(resultado, ordem);
        }
        return resultado;
    }

    private static void AdicionarDestinos(JsonArray resultado, JsonObject origem)
    {
        int posicao = 0;
        foreach (JsonNode? destino in (origem["destinos"] as JsonArray ?? []))
        {
            resultado.Add(new JsonObject
            {
                ["modalidadeOrigemCodigo"] = origem["origem"]?.DeepClone(),
                ["ordem"] = ++posicao,
                ["modalidadeDestinoCodigo"] = destino?.DeepClone()
            });
        }
    }
}
