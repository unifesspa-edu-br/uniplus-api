namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

internal static class FormularioComTermosNoEnvelope
{
    internal static JsonObject Documento(params JsonObject[] formularios) => new()
    {
        ["formularios"] = new JsonArray(formularios),
    };

    internal static JsonObject Formulario(string codigo, params JsonObject[] termos) => new()
    {
        ["codigo"] = codigo,
        ["faseId"] = null,
        ["obrigatorio"] = true,
        ["titulo"] = "Formulário fictício",
        ["consequenciaDaFaltaDeEnvio"] = null,
        ["modeloOrigem"] = null,
        ["paginas"] = new JsonArray(PaginaDeRevisao()),
        ["termos"] = new JsonArray(termos),
    };

    internal static JsonObject PaginaDeRevisao() => Pagina("REVISAO", 0, "BLOCO", "REVISAO_E_ACEITE");

    internal static JsonObject Pagina(string codigo, int ordem, string tipo, string? bloco, JsonNode? exibicao = null) => new()
    {
        ["codigo"] = codigo,
        ["ordem"] = ordem,
        ["tipo"] = tipo,
        ["bloco"] = bloco,
        ["titulo"] = "Revisão",
        ["descricao"] = null,
        ["aviso"] = null,
        ["exibicao"] = exibicao,
    };

    internal static JsonArray Exibicao() => new(new JsonArray(new JsonObject
    {
        ["codigoDoDado"] = "FATO",
        ["operador"] = "IGUAL",
        ["valor"] = true,
    }));

    internal static JsonObject Termo(string codigo, int ordem, JsonNode? exibicao = null) => new()
    {
        ["codigo"] = codigo,
        ["ordem"] = ordem,
        ["termoId"] = "11111111-1111-1111-1111-111111111111",
        ["versaoId"] = "22222222-2222-2222-2222-222222222222",
        ["termoCodigo"] = "CATALOGO",
        ["versaoNumero"] = 1,
        ["nome"] = "Termo fictício",
        ["texto"] = "Texto fictício",
        ["baseLegal"] = "Base fictícia",
        ["formaAceite"] = "REGISTRO_SEM_ENDERECO_IP",
        ["hashVersao"] = "hash fictício",
        ["exibicao"] = exibicao,
        ["obrigatoriedade"] = new JsonObject { ["tipo"] = "SEMPRE", ["quando"] = null },
    };
}
