namespace Unifesspa.UniPlus.Selecao.Application.Services.ExportacaoDaVersaoPublicada;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

internal static class ProjecaoDeDocumentosExigidos
{
    public static JsonNode? Projetar(JsonObject congelado, FormaDoNo formaDaParte, ChavesDoDocumentoCongelado chaves)
    {
        if (congelado["documentosExigidos"] is not JsonObject documentos)
        {
            return null;
        }
        JsonObject origem = (JsonObject)documentos.DeepClone();
        foreach (JsonObject exigencia in (origem["exigencias"] as JsonArray ?? []).OfType<JsonObject>())
        {
            PrepararExigencia(exigencia, chaves);
        }
        origem["regrasDeComprovacao"] = congelado["regrasDeComprovacao"]?.DeepClone();
        PrepararRegras(origem["regrasDeComprovacao"] as JsonArray, chaves);
        return CopiaPeloEsquema.Copiar(origem, formaDaParte);
    }
    private static void PrepararExigencia(JsonObject exigencia, ChavesDoDocumentoCongelado chaves)
    {
        if (exigencia["exigidoNaFaseId"] is { } fase)
        {
            exigencia["faseChave"] = chaves.Fase(fase.ToString());
        }
        if (exigencia["exigidoNaEtapaId"] is { } etapa)
        {
            exigencia["etapaChave"] = chaves.Etapa(etapa.ToString());
        }
        if (exigencia["idadeMaximaEmissao"] is JsonObject idade && idade["referenciaFaseId"] is { } referencia)
        {
            idade["referenciaFaseChave"] = chaves.Fase(referencia.ToString());
        }
    }
    private static void PrepararRegras(JsonArray? regras, ChavesDoDocumentoCongelado chaves)
    {
        if (regras is null)
        {
            return;
        }
        JsonObject[] ordenadas = regras.OfType<JsonObject>().OrderBy(regra => regra["ordem"]?.GetValue<int>()).ToArray();
        regras.Clear();
        foreach (JsonObject regra in ordenadas)
        {
            PrepararRegra(regra, chaves);
            regras.Add(regra);
        }
    }
    private static void PrepararRegra(JsonObject regra, ChavesDoDocumentoCongelado chaves)
    {
        if (regra["exigenciaId"] is { } id)
        {
            regra["exigenciaChave"] = chaves.Exigencia(id.ToString());
        }
        PrepararRegras(regra["itens"] as JsonArray, chaves);
    }
}
