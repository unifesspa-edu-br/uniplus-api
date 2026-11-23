namespace Unifesspa.UniPlus.Selecao.Application.Services.ExportacaoDaVersaoPublicada;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

internal static class ProjecaoDeDistribuicaoVagas
{
    public static JsonNode? Projetar(JsonObject congelado, FormaDoNo formaDaParte, ChavesDoDocumentoCongelado chaves)
    {
        if (congelado["distribuicao"] is not JsonArray distribuicoes || formaDaParte.Itens is not { } formaDoItem)
        {
            return null;
        }
        return new JsonArray([.. distribuicoes.OfType<JsonObject>().Select(item => ProjetarOferta(congelado, item, formaDoItem, chaves))]);
    }

    private static JsonObject ProjetarOferta(JsonObject congelado, JsonObject item, FormaDoNo forma, ChavesDoDocumentoCongelado chaves)
    {
        string identificador = item["ofertaCursoOrigemId"]!.ToString();
        JsonObject origem = (JsonObject)item.DeepClone();
        Unir(origem, chaves.Oferta(identificador));
        origem["ofertaCursoCodigo"] = chaves.CodigoDaOferta(identificador);
        origem["referenciaDemograficaCodigo"] = item["referenciaDemografica"]?["censoReferencia"]?.DeepClone();
        origem["modalidades"] = Filtrar(congelado["modalidades"] as JsonArray, identificador);
        JsonObject? vagas = Filtrar(congelado["vagas"] as JsonArray, identificador).OfType<JsonObject>().FirstOrDefault();
        if (vagas is not null)
        {
            Unir(origem, vagas);
        }
        return CopiaPeloEsquema.Copiar(origem, forma);
    }

    private static JsonArray Filtrar(JsonArray? lista, string identificador) => new([.. (lista ?? []).OfType<JsonObject>()
        .Where(item => string.Equals(item["ofertaCursoOrigemId"]?.ToString(), identificador, StringComparison.OrdinalIgnoreCase))
        .Select(item => item.DeepClone())]);

    private static void Unir(JsonObject destino, JsonObject fonte)
    {
        foreach ((string nome, JsonNode? valor) in fonte)
        {
            ProjecaoDeParte.Transferir(destino, nome, valor);
        }
    }
}
