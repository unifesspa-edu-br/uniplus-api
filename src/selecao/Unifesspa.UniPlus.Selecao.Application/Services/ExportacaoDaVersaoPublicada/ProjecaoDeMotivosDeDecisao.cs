namespace Unifesspa.UniPlus.Selecao.Application.Services.ExportacaoDaVersaoPublicada;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

internal static class ProjecaoDeMotivosDeDecisao
{
    public static JsonNode? Projetar(JsonObject congelado, FormaDoNo formaDaParte, ChavesDoDocumentoCongelado chaves)
    {
        if (congelado["motivosDeDecisao"] is not JsonArray motivos)
        {
            return null;
        }
        return new JsonArray([.. motivos.OfType<JsonObject>().Select(motivo =>
            (JsonNode)CopiaPeloEsquema.Copiar(Preparar(motivo, chaves), formaDaParte.Itens!))]);
    }
    private static JsonObject Preparar(JsonObject motivo, ChavesDoDocumentoCongelado chaves)
    {
        JsonObject origem = (JsonObject)motivo.DeepClone();
        string tipo = motivo["lugar"]?["tipo"]?.ToString() ?? string.Empty;
        string id = motivo["lugar"]?["id"]?.ToString() ?? string.Empty;
        switch (tipo)
        {
            case "FASE": origem["faseChave"] = chaves.FasePeloTipo(id); break;
            case "ETAPA": origem["etapaChave"] = chaves.Etapa(id); break;
            default: throw new InvalidOperationException($"Lugar de decisão inválido: '{tipo}'.");
        }
        return origem;
    }
}
