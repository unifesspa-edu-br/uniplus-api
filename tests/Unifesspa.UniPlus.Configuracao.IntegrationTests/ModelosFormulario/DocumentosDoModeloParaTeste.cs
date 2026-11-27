namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.ModelosFormulario;

using System.Text.Json.Nodes;

/// <summary>Os corpos do conjunto de documentos do modelo de formulário que os testes enviam.</summary>
internal static class DocumentosDoModeloParaTeste
{
    internal static JsonObject GrupoDeDocumentos() => new()
    {
        ["quantidadeMinima"] = 1,
        ["indeferimento"] = new JsonObject { ["consequenciaDoDescumprimento"] = "ELIMINA_DO_PROCESSO", ["abrePrazoParaReenvio"] = false, ["basesLegais"] = new JsonArray() },
        ["documentos"] = new JsonArray(Documento()),
    };

    internal static JsonObject ConjuntoCom(JsonArray documentos, JsonArray? grupos = null) =>
        new() { ["documentosExigidos"] = documentos, ["gruposDeDocumentos"] = grupos ?? new JsonArray() };

    internal static JsonObject Documento(string? faseCodigo = "HABILITACAO", JsonNode? quando = null, string? porOpcaoDoFato = null) => new()
    {
        ["faseCodigo"] = faseCodigo,
        ["tipoDocumentoCodigo"] = "COMPROVANTE_RESIDENCIA",
        ["quando"] = quando,
        ["indeferimento"] = new JsonObject { ["obrigatorio"] = true, ["consequenciaDoDescumprimento"] = "ELIMINA_DO_PROCESSO", ["abrePrazoParaReenvio"] = false, ["basesLegais"] = new JsonArray() },
        ["porOpcao"] = porOpcaoDoFato is null ? null : new JsonObject { ["fatoCodigo"] = porOpcaoDoFato, ["exceto"] = new JsonArray() },
    };
}
