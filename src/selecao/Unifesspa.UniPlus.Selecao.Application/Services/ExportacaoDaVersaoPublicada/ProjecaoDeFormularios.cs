namespace Unifesspa.UniPlus.Selecao.Application.Services.ExportacaoDaVersaoPublicada;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

internal static class ProjecaoDeFormularios
{
    public static JsonNode? Projetar(JsonObject congelado, FormaDoNo formaDaParte, ChavesDoDocumentoCongelado chaves)
    {
        if (congelado["formularios"] is not JsonArray formularios)
        {
            return null;
        }
        return new JsonArray([.. formularios.OfType<JsonObject>().Select(formulario =>
            (JsonNode)CopiaPeloEsquema.Copiar(Preparar(formulario, congelado, chaves), formaDaParte.Itens!))]);
    }
    private static JsonObject Preparar(JsonObject formulario, JsonObject congelado, ChavesDoDocumentoCongelado chaves)
    {
        JsonObject origem = (JsonObject)formulario.DeepClone();
        if (formulario["faseId"] is { } fase)
        {
            origem["faseChave"] = chaves.Fase(fase.ToString());
        }
        TransferirModeloDeOrigem(origem, formulario);
        origem["itens"] = DaFinalidade(congelado["dadosDosFormularios"] as JsonArray, formulario);
        origem["grupos"] = DaFinalidade(congelado["gruposDosFormularios"] as JsonArray, formulario);
        PrepararItens(origem["itens"] as JsonArray);
        foreach (JsonObject grupo in (origem["grupos"] as JsonArray ?? []).OfType<JsonObject>())
        {
            PrepararItens(grupo["subitens"] as JsonArray);
        }
        return origem;
    }
    private static void TransferirModeloDeOrigem(JsonObject origem, JsonObject formulario)
    {
        ProjecaoDeParte.Transferir(origem, "modeloOrigemCodigo", formulario["modeloOrigem"]?["codigo"]);
        ProjecaoDeParte.Transferir(origem, "modeloOrigemNome", formulario["modeloOrigem"]?["nome"]);
    }
    private static JsonArray DaFinalidade(JsonArray? itens, JsonObject formulario) => new([.. (itens ?? []).OfType<JsonObject>()
        .Where(item => item["finalidade"]?.ToString() == formulario["finalidade"]?.ToString())
        .OrderBy(item => item["ordem"]?.GetValue<int>()).Select(item => item.DeepClone())]);
    private static void PrepararItens(JsonArray? itens)
    {
        foreach (JsonObject item in (itens ?? []).OfType<JsonObject>())
        {
            PrepararItem(item);
        }
    }
    private static void PrepararItem(JsonObject item)
    {
        string codigo = item["codigoDoDado"]?.ToString() ?? string.Empty;
        if (item["origemValores"]?.ToString() != OrigemValoresColeta.OpcoesDoProcesso.Codigo() || AtendimentoEspecializado.GereOpcoesDoFato(codigo))
        {
            return;
        }
        JsonArray valores = item["valoresSelecionaveis"] as JsonArray ?? [];
        item["opcoes"] = new JsonArray([.. valores.OfType<JsonObject>().OrderBy(valor => valor["ordem"]?.GetValue<int>())
            .Select(valor => (JsonNode)new JsonObject { ["codigo"] = valor["valorCodigo"]?.DeepClone(), ["rotulo"] = valor["descricao"]?.DeepClone() })]);
    }
}
