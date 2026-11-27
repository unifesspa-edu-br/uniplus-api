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
        if (origem["quadroPesoAreaEnem"] is JsonArray quadro)
        {
            origem["quadroPesoAreaEnem"] = ProjetarQuadroDePesos(quadro, formaDaParte);
        }
        TraduzirEtapas(origem["regrasEliminacao"] as JsonArray, chaves);
        TraduzirEtapas(origem["criteriosDeDesempate"] as JsonArray, chaves);
        return CopiaPeloEsquema.Copiar(origem, formaDaParte);
    }

    /// <summary>
    /// O quadro de pesos do documento congelado na forma da exportação: o grupo de áreas achatado em código e rótulo, o peso e
    /// o corte como número e o corte nulo descartado, tudo conforme o que o esquema declara para a classificação.
    /// </summary>
    internal static JsonArray ProjetarQuadroDePesos(JsonArray quadroCongelado, FormaDoNo formaDaClassificacao)
    {
        FormaDoNo formaDoGrupo = formaDaClassificacao.Propriedades["quadroPesoAreaEnem"].Itens!;
        return new([.. quadroCongelado.OfType<JsonObject>().Select(grupo => (JsonNode)CopiaPeloEsquema.Copiar(AchatarGrupo(grupo), formaDoGrupo))]);
    }

    private static JsonObject AchatarGrupo(JsonObject grupo)
    {
        JsonObject achatado = (JsonObject)grupo.DeepClone();
        ProjecaoDeParte.Transferir(achatado, "grupoAreaEnemCodigo", grupo["grupoAreaEnem"]?["codigo"]);
        ProjecaoDeParte.Transferir(achatado, "grupoAreaEnemRotulo", grupo["grupoAreaEnem"]?["rotulo"]);
        return achatado;
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
