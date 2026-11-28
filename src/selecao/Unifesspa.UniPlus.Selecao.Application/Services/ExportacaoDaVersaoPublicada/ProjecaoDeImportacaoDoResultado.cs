namespace Unifesspa.UniPlus.Selecao.Application.Services.ExportacaoDaVersaoPublicada;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

internal static class ProjecaoDeImportacaoDoResultado
{
    private const string Bloco = "importacaoDoResultado";

    public static JsonNode? Projetar(JsonObject congelado, FormaDoNo formaDaParte, ChavesDoDocumentoCongelado chaves)
    {
        if (congelado[Bloco] is not JsonObject importacao)
        {
            return null;
        }

        JsonObject origem = new() { ["relatorios"] = importacao["relatorios"]?.DeepClone() };
        if (importacao["primeiraFaseDepoisDaImportacaoId"] is { } identificador)
        {
            origem["primeiraFaseDepoisDaImportacaoChave"] = chaves.Fase(identificador.ToString());
        }

        return CopiaPeloEsquema.Copiar(origem, formaDaParte);
    }
}
