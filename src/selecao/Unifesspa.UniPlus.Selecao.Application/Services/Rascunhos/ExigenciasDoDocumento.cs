namespace Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos;

using System.Text.Json.Nodes;

internal static class ExigenciasDoDocumento
{
    public static JsonArray? Lista(JsonObject documento) => documento["documentosExigidos"]?["exigencias"] as JsonArray;

    public static string PonteiroDoModelo(int posicao) => $"/documentosExigidos/exigencias/{posicao}/modeloId";
}
