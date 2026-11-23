namespace Unifesspa.UniPlus.Selecao.Application.Services.ExportacaoDaVersaoPublicada;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

internal static class ProjecaoDeClassificacao
{
    public static JsonNode? Projetar(JsonObject congelado, FormaDoNo formaDaParte, ChavesDoDocumentoCongelado chaves)
    {
        if (congelado["classificacao"] is not JsonObject classificacao)
        {
            return null;
        }
        JsonObject origem = (JsonObject)classificacao.DeepClone();
        origem["criteriosDeDesempate"] = congelado["criteriosDesempate"]?.DeepClone();
        AchatarGrupos(origem["quadroPesoAreaEnem"] as JsonArray);
        TraduzirEtapas(origem["regrasEliminacao"] as JsonArray, chaves);
        TraduzirEtapas(origem["criteriosDeDesempate"] as JsonArray, chaves);
        return CopiaPeloEsquema.Copiar(origem, formaDaParte);
    }

    private static void AchatarGrupos(JsonArray? grupos)
    {
        foreach (JsonObject grupo in (grupos ?? []).OfType<JsonObject>())
        {
            ProjecaoDeParte.Transferir(grupo, "grupoAreaEnemCodigo", grupo["grupoAreaEnem"]?["codigo"]);
            ProjecaoDeParte.Transferir(grupo, "grupoAreaEnemRotulo", grupo["grupoAreaEnem"]?["rotulo"]);
        }
    }
    private static void TraduzirEtapas(JsonArray? regras, ChavesDoDocumentoCongelado chaves)
    {
        foreach (JsonObject regra in (regras ?? []).OfType<JsonObject>())
        {
            if (regra["parametros"] is JsonObject parametros && parametros["etapaId"] is { } identificador)
            {
                parametros["etapaChave"] = chaves.Etapa(identificador.ToString());
            }
        }
    }

}
