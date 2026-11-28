namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.TestSupport;

public sealed class GruposRecusadosNoEnvelopeTests
{
    [Fact]
    public void LerGruposColetados_SubitemComFormularioProprio_NaoRepeteValor()
    {
        const string valor = "EXEMPLO_DADO_RECUSADO";
        JsonObject campo = Campo(valor);
        campo["formularioCodigo"] = FormulariosDoDocumentoDeTeste.CodigoDaHabilitacao;
        JsonObject documento = Documento(campo, new JsonArray());
        LeitorEnvelope leitor = new();
        LeituraDosGrupos.LerGruposColetados(leitor, documento, new(StringComparer.Ordinal), [], FormulariosDoDocumentoDeTeste.ComFormularios());
        Conferir(leitor, valor, "'gruposDosFormularios[0].subitens': um campo tem formulário próprio, mas o campo do grupo segue o formulário do grupo.", "gruposDosFormularios[0].subitens");
    }

    [Fact]
    public void LerGruposColetados_FatoDeMembroAusente_NaoRepeteValor()
    {
        const string valor = "EXEMPLO_MEMBRO_AUSENTE";
        JsonArray agregados = new(new JsonObject { ["codigo"] = "AGREGADO", ["codigoDoDadoDoMembro"] = valor, ["operacao"] = "EXISTE" });
        JsonObject documento = Documento(Campo("DADO"), agregados);
        LeitorEnvelope leitor = new();
        LeituraDosGrupos.LerGruposColetados(leitor, documento, new(StringComparer.Ordinal), [], FormulariosDoDocumentoDeTeste.ComFormularios());
        Conferir(leitor, valor, "'gruposDosFormularios[0].agregados[0]': o fato de membro não é campo do grupo.", null);
    }

    private static void Conferir(LeitorEnvelope leitor, string valor, string mensagem, string? campo)
    {
        FieldError causa = leitor.Falha<object>().Errors.Should().ContainSingle().Subject;
        causa.Error.Code.Should().Be("EnvelopeCodec.EnvelopeMalformado");
        causa.Error.Message.Should().Be(mensagem).And.NotContain(valor);
        causa.Field.Should().Be(campo);
    }

    private static JsonObject Documento(JsonObject campo, JsonArray agregados) => new()
    {
        ["gruposDosFormularios"] = new JsonArray(Grupo(campo, agregados)),
    };

    private static JsonObject Grupo(JsonObject campo, JsonArray agregados) => new()
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
        ["subitens"] = new JsonArray(campo),
        ["agregados"] = agregados,
    };

    private static JsonObject Campo(string codigo)
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
