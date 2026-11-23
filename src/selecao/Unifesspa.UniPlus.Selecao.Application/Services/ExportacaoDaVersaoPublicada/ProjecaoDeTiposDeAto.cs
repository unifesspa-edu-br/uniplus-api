namespace Unifesspa.UniPlus.Selecao.Application.Services.ExportacaoDaVersaoPublicada;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

internal static class ProjecaoDeTiposDeAto
{
    public static JsonNode? Projetar(JsonObject congelado, FormaDoNo formaDaParte, ChavesDoDocumentoCongelado chaves)
    {
        if (congelado["tiposDeAto"] is not JsonArray tipos || formaDaParte.Itens is not { } formaDoItem)
        {
            return null;
        }
        return new JsonArray([.. tipos.OfType<JsonObject>().Select(item => (JsonNode)CopiaPeloEsquema.Copiar(item, formaDoItem))]);
    }
}
