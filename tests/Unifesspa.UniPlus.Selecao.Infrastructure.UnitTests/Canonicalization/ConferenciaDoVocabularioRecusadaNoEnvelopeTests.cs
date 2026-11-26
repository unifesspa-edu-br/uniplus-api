namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.TestSupport;

public sealed class ConferenciaDoVocabularioRecusadaNoEnvelopeTests
{
    [Theory]
    [InlineData(0, "EXEMPLO_FATO_REPETIDO", "um fato aparece mais de uma vez")]
    [InlineData(1, "987654", "a mesma ordem é usada por mais de um fato")]
    [InlineData(2, "EXEMPLO_GRUPO_REPETIDO", "o mesmo código aparece em mais de um grupo")]
    [InlineData(3, "987654", "a mesma ordem é usada por mais de um item ou grupo")]
    [InlineData(4, "EXEMPLO_FATO_REPETIDO", "um fato aparece mais de uma vez")]
    [InlineData(5, "987654", "a mesma ordem é usada por mais de um campo")]
    public void ConferirCodigosEOrdens_DuplicidadeNoDocumento_NaoRepeteValor(int caso, string valor, string trecho)
    {
        JsonObject documento = Documento(caso);
        documento.ToJsonString().Should().Contain(valor);
        LeitorEnvelope leitor = new();
        FormulariosDoDocumento formularios = FormulariosDoDocumentoDeTeste.ComFormularios();
        (IReadOnlyList<FatoColetado> fatos, Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> valores) = LeituraDosFatos.LerFatosColetados(leitor, documento, formularios);
        IReadOnlyList<GrupoColetado> grupos = LeituraDosGrupos.LerGruposColetados(leitor, documento, valores, [], formularios);
        leitor.Falhou.Should().BeFalse();
        DomainError erro = ConferenciaDoVocabularioDeFatos.ConferirCodigosEOrdens(new(fatos, grupos, [], "1"), [], [])!;
        Conferir(leitor, erro, valor, trecho);
    }

    [Theory]
    [InlineData("MODALIDADE")]
    [InlineData("OPTOU_PELAS_COTAS_DA_LEI")]
    public void ConferirDerivados_FatoDaOfertaNoDocumento_NaoRepeteCodigo(string valor)
    {
        JsonObject documento = Derivacao(valor, null);
        LeitorEnvelope leitor = new();
        IReadOnlyList<ConfiguracaoDerivacaoFato> regras = LeituraDaDerivacao.LerRegrasDerivacao(leitor, documento);
        leitor.Falhou.Should().BeFalse();
        DomainError erro = ConferenciaDoVocabularioDeFatos.ConferirDerivados(regras, [], [], [])!;
        Conferir(leitor, erro, valor, "um fato derivado da oferta");
    }

    [Fact]
    public void ConferirCitacoes_FatoAusenteNoDocumento_NaoRepeteCodigo()
    {
        const string valor = "EXEMPLO_CITACAO_AUSENTE";
        JsonObject documento = Derivacao("DERIVADO", valor);
        LeitorEnvelope leitor = new();
        IReadOnlyList<ConfiguracaoDerivacaoFato> regras = LeituraDaDerivacao.LerRegrasDerivacao(leitor, documento);
        leitor.Falhou.Should().BeFalse();
        DomainError erro = ConferenciaDoVocabularioDeFatos.ConferirCitacoes(regras, [], [])!;
        Conferir(leitor, erro, valor, "cita um fato que o processo não coleta nem deriva");
    }

    private static void Conferir(LeitorEnvelope leitor, DomainError erro, string valor, string trecho)
    {
        leitor.Propagar<object>(erro);
        FieldError causa = leitor.Falha<object>().Errors.Should().ContainSingle().Subject;
        causa.Error.Code.Should().Be("EnvelopeCodec.EnvelopeMalformado");
        causa.Error.Message.Should().Contain(trecho).And.NotContainEquivalentOf(valor);
        causa.Field.Should().BeNull();
    }

    private static JsonObject Derivacao(string codigo, string? citacao) => new()
    {
        ["calculosDosDados"] = new JsonArray(new JsonObject
        {
            ["codigoDoDado"] = codigo,
            ["regras"] = new JsonArray(new JsonObject
            {
                ["ordem"] = 0,
                ["contribui"] = null,
                ["quando"] = citacao is null ? null :
                new JsonArray(new JsonArray(new JsonObject { ["codigoDoDado"] = citacao, ["operador"] = "IGUAL", ["valor"] = "SIM" }))
            }),
        }),
    };

    private static JsonObject Documento(int caso)
    {
        JsonArray fatos = caso switch
        {
            0 => new JsonArray(Fato("EXEMPLO_FATO_REPETIDO", 0), Fato("EXEMPLO_FATO_REPETIDO", 1)),
            1 => new JsonArray(Fato("FATO_A", 987654), Fato("FATO_B", 987654)),
            3 => new JsonArray(Fato("FATO_A", 987654)),
            _ => [],
        };
        JsonArray grupos = Grupos(caso);
        return new JsonObject { ["dadosDosFormularios"] = fatos, ["gruposDosFormularios"] = grupos };
    }

    private static JsonArray Grupos(int caso) => caso switch
    {
        2 => new JsonArray(Grupo("EXEMPLO_GRUPO_REPETIDO", 0, Fato("A", 0, true)), Grupo("EXEMPLO_GRUPO_REPETIDO", 1, Fato("B", 0, true))),
        3 => new JsonArray(Grupo("GRUPO", 987654, Fato("B", 0, true))),
        4 => new JsonArray(Grupo("GRUPO", 0, Fato("EXEMPLO_FATO_REPETIDO", 0, true), Fato("EXEMPLO_FATO_REPETIDO", 1, true))),
        5 => new JsonArray(Grupo("GRUPO", 0, Fato("A", 987654, true), Fato("B", 987654, true))),
        _ => [],
    };

    internal static JsonObject Fato(string codigo, int ordem, bool emGrupo = false)
    {
        JsonObject fato = JsonNode.Parse("""
            {"ajuda":null,"classificacaoProtecao":"PESSOAL","codigoDoDado":"DADO","componenteExibicao":null,
            "descricao":"Descrição","exibicao":null,"formularioCodigo":"INSCRICAO","finalidadeTratamento":"Finalidade",
            "formato":"LIVRE","hipoteseLegal":"CUMPRIMENTO_OBRIGACAO_LEGAL","impedimento":null,
            "obrigatoriedade":{"quando":null,"tipo":"SEMPRE"},"ordem":0,"origemValores":"CATALOGO",
            "paginaCodigo":"PAGINA","pedirConfirmacao":false,"restricoes":[],"rotulo":"Dado","tipoDeCampo":"TEXTO","valoresSelecionaveis":null}
            """)!.AsObject();
        fato["codigoDoDado"] = codigo;
        fato["ordem"] = ordem;
        if (emGrupo) { fato["paginaCodigo"] = null; }
        return fato;
    }

    private static JsonObject Grupo(string codigo, int ordem, params JsonObject[] campos) => new()
    {
        ["agregados"] = new JsonArray(),
        ["codigo"] = codigo,
        ["exibicao"] = null,
        ["formularioCodigo"] = "INSCRICAO",
        ["incluiCandidato"] = false,
        ["maximo"] = 2,
        ["minimo"] = 1,
        ["obrigatoriedade"] = new JsonObject { ["quando"] = null, ["tipo"] = "SEMPRE" },
        ["ordem"] = ordem,
        ["paginaCodigo"] = "PAGINA",
        ["rotulo"] = "Grupo",
        ["subitens"] = new JsonArray(campos),
    };
}
