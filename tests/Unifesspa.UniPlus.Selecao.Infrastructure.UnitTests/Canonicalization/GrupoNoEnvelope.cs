namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.TestSupport;

/// <summary>Um documento com um único grupo íntegro e um campo íntegro, para cada teste estragar só o que verifica.</summary>
internal static class GrupoNoEnvelope
{
    internal static JsonObject Documento(JsonObject grupo) => new() { ["gruposDosFormularios"] = new JsonArray(grupo) };

    internal static JsonObject Grupo(params JsonObject[] campos) => new()
    {
        ["codigo"] = "GRUPO",
        ["formularioCodigo"] = FormulariosDoDocumentoDeTeste.CodigoDaInscricao,
        ["paginaCodigo"] = "DADOS",
        ["ordem"] = 0,
        ["rotulo"] = "Grupo",
        ["minimo"] = 1,
        ["maximo"] = 2,
        ["incluiCandidato"] = false,
        ["exibicao"] = null,
        ["obrigatoriedade"] = new JsonObject { ["tipo"] = "SEMPRE", ["quando"] = null },
        ["subitens"] = new JsonArray([.. campos]),
        ["agregados"] = new JsonArray(),
    };

    internal static JsonObject Campo(string codigo = "DADO")
    {
        JsonObject campo = JsonNode.Parse("""
            {"codigoDoDado":"DADO","formularioCodigo":"INSCRICAO","paginaCodigo":null,"ordem":0,"rotulo":"Dado",
             "tipoDeCampo":"TEXTO","obrigatoriedade":{"tipo":"SEMPRE","quando":null},"ajuda":null,
             "pedirConfirmacao":false,"restricoes":[],"impedimento":null,"origemValores":"CATALOGO","formato":"LIVRE",
             "classificacaoProtecao":"PESSOAL","hipoteseLegal":"CUMPRIMENTO_OBRIGACAO_LEGAL",
             "finalidadeTratamento":"Finalidade","descricao":"Descrição","componenteExibicao":null,
             "exibicao":null,"valoresSelecionaveis":null}
            """)!.AsObject();
        campo["codigoDoDado"] = codigo;
        return campo;
    }
}
