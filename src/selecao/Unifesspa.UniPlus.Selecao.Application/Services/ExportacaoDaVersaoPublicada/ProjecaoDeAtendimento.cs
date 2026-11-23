namespace Unifesspa.UniPlus.Selecao.Application.Services.ExportacaoDaVersaoPublicada;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

internal static class ProjecaoDeAtendimento
{
    public static JsonNode? Projetar(JsonObject congelado, FormaDoNo formaDaParte, ChavesDoDocumentoCongelado chaves) =>
        ProjecaoDeParte.CopiarObjeto(congelado, "atendimento", formaDaParte);
}
