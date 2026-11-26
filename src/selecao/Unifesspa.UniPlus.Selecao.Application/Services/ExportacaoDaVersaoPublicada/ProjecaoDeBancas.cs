namespace Unifesspa.UniPlus.Selecao.Application.Services.ExportacaoDaVersaoPublicada;

using System.Text.Json.Nodes;

internal static class ProjecaoDeBancas
{
    public static void PrepararDaFase(JsonObject origem) => Preparar(origem, "bancasRequeridas", PrepararBancaDaFase);

    public static void PrepararDaEtapa(JsonObject origem) => Preparar(origem, "bancas", PrepararBanca);

    private static void Preparar(JsonObject origem, string campo, Func<JsonObject, JsonNode> preparar)
    {
        if (origem[campo] is JsonArray bancas)
        {
            origem[campo] = new JsonArray([.. bancas.OfType<JsonObject>().Select(preparar)]);
        }
    }

    private static JsonNode PrepararBancaDaFase(JsonObject banca)
    {
        JsonObject copia = PrepararBanca(banca);
        if (banca["candidatosAtendidos"] is JsonArray candidatos)
        {
            if (candidatos.OfType<JsonObject>().All(candidato => candidato["codigo"] is not null))
            {
                copia["categoriasDocumentoCodigos"] = Valores(candidatos, "codigo");
            }
            if (candidatos.OfType<JsonObject>().All(candidato => candidato["nome"] is not null))
            {
                copia["categoriasDocumentoNomes"] = Valores(candidatos, "nome");
            }
        }
        return copia;
    }

    private static JsonObject PrepararBanca(JsonObject banca)
    {
        JsonObject copia = (JsonObject)banca.DeepClone();
        ProjecaoDeParte.Transferir(copia, "tipoBancaCodigo", banca["codigo"]);
        ProjecaoDeParte.Transferir(copia, "tipoBancaNome", banca["nome"]);
        ProjecaoDeParte.Transferir(copia, "consequenciaDaAusenciaRotulo", banca["contexto"]?["consequenciaDaAusenciaRotulo"]);
        return copia;
    }

    private static JsonArray Valores(JsonArray candidatos, string campo) =>
        new([.. candidatos.OfType<JsonObject>().Select(candidato => candidato[campo]?.DeepClone())]);
}
