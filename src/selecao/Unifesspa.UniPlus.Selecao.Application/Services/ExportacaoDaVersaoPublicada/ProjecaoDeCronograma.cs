namespace Unifesspa.UniPlus.Selecao.Application.Services.ExportacaoDaVersaoPublicada;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

internal static class ProjecaoDeCronograma
{
    public static JsonNode? Projetar(JsonObject congelado, FormaDoNo formaDaParte, ChavesDoDocumentoCongelado chaves)
    {
        if (congelado["cronogramaFases"]?["fases"] is not JsonArray fases)
        {
            return null;
        }
        JsonArray itens = new([.. fases.OfType<JsonObject>().OrderBy(fase => fase["ordem"]?.GetValue<int>()).Select(fase => PrepararFase(fase, congelado, chaves))]);
        return CopiaPeloEsquema.Copiar(new JsonObject { ["fases"] = itens }, formaDaParte);
    }
    private static JsonObject PrepararFase(JsonObject fase, JsonObject congelado, ChavesDoDocumentoCongelado chaves)
    {
        JsonObject origem = (JsonObject)fase.DeepClone();
        ProjecaoDeParte.Transferir(origem, "tipoFaseCodigo", fase["codigo"]);
        ProjecaoDeParte.Transferir(origem, "unidadeResponsavelCodigo", fase["unidadeResponsavel"]?["codigo"]);
        ProjecaoDeParte.Transferir(origem, "unidadeResponsavelNome", fase["unidadeResponsavel"]?["nome"]);
        ProjecaoDeParte.Transferir(origem, "unidadeResponsavelSigla", fase["unidadeResponsavel"]?["sigla"]);
        ProjecaoDeBancas.PrepararDaFase(origem);
        ProjecaoDeRecursos.Preparar(origem, chaves);
        ProjecaoDePublicacoesPrevistas.Preparar(origem, congelado);
        return origem;
    }
}
