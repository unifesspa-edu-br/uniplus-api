namespace Unifesspa.UniPlus.Selecao.Application.Services.ExportacaoDaVersaoPublicada;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

internal static class ProjecaoDeEtapas
{
    public static JsonNode? Projetar(JsonObject congelado, FormaDoNo formaDaParte, ChavesDoDocumentoCongelado chaves)
    {
        if (congelado["etapas"] is not JsonArray etapas)
        {
            return null;
        }
        return new JsonArray([.. etapas.OfType<JsonObject>().OrderBy(etapa => etapa["ordem"]?.GetValue<int>())
            .Select(etapa => (JsonNode)CopiaPeloEsquema.Copiar(Preparar(etapa, congelado, chaves), formaDaParte.Itens!))]);
    }
    private static JsonObject Preparar(JsonObject etapa, JsonObject congelado, ChavesDoDocumentoCongelado chaves)
    {
        JsonObject origem = (JsonObject)etapa.DeepClone();
        ProjecaoDeParte.Transferir(origem, "tipoEtapaCodigo", etapa["tipoEtapa"]?["codigo"]);
        if (etapa["carater"] is { } carater)
            origem["carater"] = carater.ToString().ToLowerInvariant();
        if (etapa["faseCodigo"] is { } fase)
        {
            origem["faseChave"] = chaves.FasePeloCodigo(fase.ToString());
        }
        ProjecaoDeBancas.PrepararDaEtapa(origem);
        ProjecaoDeRecursos.Preparar(origem, chaves);
        ProjecaoDePublicacoesPrevistas.Preparar(origem, congelado);
        Ordenar(origem, "itensDeAvaliacao");
        Ordenar(origem, "escalaDeAtendimento");
        return origem;
    }

    private static void Ordenar(JsonObject origem, string nome)
    {
        if (origem[nome] is JsonArray itens)
        {
            origem[nome] = new JsonArray([.. itens.OrderBy(item => item?["ordem"]?.GetValue<int>())
                .Select(item => item?.DeepClone())]);
        }
    }
}
