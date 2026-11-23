namespace Unifesspa.UniPlus.Selecao.Application.Services.ExportacaoDaVersaoPublicada;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

internal static class ProjecaoDeParte
{
    public static JsonObject? CopiarObjeto(JsonObject congelado, string nome, FormaDoNo forma) =>
        congelado[nome] is JsonObject objeto ? CopiaPeloEsquema.Copiar(objeto, forma) : null;

    public static void Transferir(JsonObject destino, string nomeDestino, JsonNode? valor)
    {
        if (valor is not null)
        {
            destino[nomeDestino] = valor.DeepClone();
        }
    }
}
