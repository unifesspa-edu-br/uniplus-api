namespace Unifesspa.UniPlus.Selecao.Application.Services.ExportacaoDaVersaoPublicada;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

internal static class ProjecaoDeColeta
{
    public static JsonNode? Projetar(JsonObject congelado, FormaDoNo formaDaParte, ChavesDoDocumentoCongelado chaves)
    {
        JsonNode? referencia = congelado["documentosExigidos"]?["referenciaTemporalDosDados"]?.DeepClone();
        if (congelado["calculosDosDados"] is not JsonArray && referencia is null)
        {
            return null;
        }
        JsonObject origem = new() { ["calculosDosDados"] = congelado["calculosDosDados"]?.DeepClone() };
        if (referencia is JsonObject objeto && objeto["faseId"] is { } identificador)
        {
            objeto["faseChave"] = chaves.Fase(identificador.ToString());
        }
        origem["referenciaTemporalDosDados"] = referencia;
        return CopiaPeloEsquema.Copiar(origem, formaDaParte);
    }
}
