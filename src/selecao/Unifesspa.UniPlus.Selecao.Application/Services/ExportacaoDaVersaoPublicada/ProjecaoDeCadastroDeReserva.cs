namespace Unifesspa.UniPlus.Selecao.Application.Services.ExportacaoDaVersaoPublicada;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

internal static class ProjecaoDeCadastroDeReserva
{
    public static JsonNode? Projetar(JsonObject congelado, FormaDoNo formaDaParte, ChavesDoDocumentoCongelado chaves) =>
        congelado["cadastroDeReserva"] is JsonArray chamadas
            ? new JsonArray([.. chamadas.OfType<JsonObject>().Select(chamada => (JsonNode)CopiaPeloEsquema.Copiar(chamada, formaDaParte.Itens!))])
            : null;
}
