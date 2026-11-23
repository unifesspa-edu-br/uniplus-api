namespace Unifesspa.UniPlus.Selecao.Application.Services.ExportacaoDaVersaoPublicada;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

internal static class ProjecaoDeFada
{
    public static JsonNode? Projetar(JsonObject congelado, FormaDoNo formaDaParte, ChavesDoDocumentoCongelado chaves)
    {
        if (congelado["fada"] is not JsonObject fada)
        {
            return null;
        }
        JsonObject origem = (JsonObject)fada.DeepClone();
        origem["aplica"] = fada["presente"]?.DeepClone();
        return CopiaPeloEsquema.Copiar(origem, formaDaParte);
    }
}
