namespace Unifesspa.UniPlus.Selecao.Application.Services.ExportacaoDaVersaoPublicada;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

internal static class ProjecaoDeTaxaDeInscricao
{
    public static JsonNode? Projetar(JsonObject congelado, FormaDoNo formaDaParte, ChavesDoDocumentoCongelado chaves) =>
        congelado["taxaInscricao"] is JsonObject taxa && taxa["presente"]?.GetValue<bool>() == true
            ? CopiaPeloEsquema.Copiar(taxa, formaDaParte) : null;
}
