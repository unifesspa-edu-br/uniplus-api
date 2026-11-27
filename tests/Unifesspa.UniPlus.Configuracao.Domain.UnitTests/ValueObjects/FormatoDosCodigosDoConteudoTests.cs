namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;

using static Unifesspa.UniPlus.Configuracao.Domain.UnitTests.TestSupport.ConteudosDoModeloDeTeste;
using static Unifesspa.UniPlus.Configuracao.Domain.UnitTests.TestSupport.DeclaracoesDeModeloDeTeste;

public sealed class FormatoDosCodigosDoConteudoTests
{
    [Theory]
    [InlineData("CODIGO_2")]
    [InlineData("A")]
    public void Conferir_CodigoValido_NaoRecusaFormato(string codigo) =>
        Erros(codigo).Should().NotContain(e => e.Error.Code == ModeloFormularioErrorCodes.CodigoDoConteudoFormatoInvalido);

    [Theory]
    [InlineData(" CODIGO")]
    [InlineData("CODIGO ")]
    [InlineData("codigo")]
    [InlineData("CÓDIGO")]
    [InlineData("CODIGO__DOIS")]
    public void Conferir_CodigoInvalido_RecusaSemValor(string codigo)
    {
        FieldError erro = Erros(codigo).Should().ContainSingle(e => e.Field == "conteudo.pressupostos[0]"
            && e.Error.Code == ModeloFormularioErrorCodes.CodigoDoConteudoFormatoInvalido).Subject;
        erro.Error.Message.Should().NotContain(codigo);
    }

    [Fact]
    public void Conferir_LimiteDoTipoDeDocumento_RecusaSomenteAcimaDoLimite()
    {
        ErrosDoDocumento(new string('A', 50)).Should().NotContain(e => e.Error.Code == ModeloFormularioErrorCodes.CodigoDoConteudoFormatoInvalido);
        ErrosDoDocumento(new string('A', 51)).Should().Contain(e => e.Field!.EndsWith("tipoDocumentoCodigo", StringComparison.Ordinal)
            && e.Error.Code == ModeloFormularioErrorCodes.CodigoDoConteudoFormatoInvalido);
    }

    [Fact]
    public void Conferir_CodigosInvalidos_AcumulaRecusas()
    {
        ConteudoDoModelo conteudo = Vazio with { Pressupostos = [" primeiro", "segundo"] };
        Conferir(conteudo).Count(e => e.Error.Code == ModeloFormularioErrorCodes.CodigoDoConteudoFormatoInvalido).Should().Be(2);
    }

    [Fact]
    public void Conferir_CodigosDoDocumentoInvalidos_AcumulaCadaCampo()
    {
        DocumentoExigidoDoModelo documento = Documento(" qztipo", requisitos: RequisitosAPartirDasFases(" qzprazo", " qzvalidade"),
            porOpcao: new ExigenciaPorOpcao(" qzfato", [" qzopcao"])) with
        { FaseCodigo = " qzfase" };
        ConteudoDoModelo conteudo = Vazio with { ConjuntoDeDocumentos = Conjunto(documento) };
        List<FieldError> erros = Conferir(conteudo).Where(e => e.Error.Code == ModeloFormularioErrorCodes.CodigoDoConteudoFormatoInvalido).ToList();
        erros.Should().HaveCount(6);
        erros.Should().OnlyContain(e => !e.Error.Message.Contains("qz", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "O documento lido com todos os códigos inválidos tem uma recusa por código, no caminho dado")]
    public void ConferirDocumento_CodigosInvalidos_RecusaCadaCodigoNoCaminho()
    {
        DocumentoExigidoDoModelo documento = Documento(" qztipo", requisitos: RequisitosAPartirDasFases(" qzprazo", " qzvalidade"),
            porOpcao: new ExigenciaPorOpcao(" qzfato", [" qzopcao"])) with
        { FaseCodigo = " qzfase" };

        List<FieldError> erros = [.. FormatoDosCodigosDoConteudo.ConferirDocumento(documento, "documentos[3]")];

        erros.Select(static e => e.Field).Should().Equal(
            "documentos[3].faseCodigo", "documentos[3].tipoDocumentoCodigo",
            "documentos[3].requisitos.prazoDeEmissao.referencia.faseCodigo", "documentos[3].requisitos.validade.referencia.faseCodigo",
            "documentos[3].porOpcao.fatoCodigo", "documentos[3].porOpcao.exceto[0]");
        erros.Should().OnlyContain(static e => !e.Error.Message.Contains("qz", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "O documento lido com códigos válidos não tem recusa")]
    public void ConferirDocumento_CodigosValidos_SemRecusa() =>
        FormatoDosCodigosDoConteudo.ConferirDocumento(Documento("CERTIDAO"), "documentos[0]").Should().BeEmpty();

    [Fact(DisplayName = "O documento lido com a fase em branco recusa a fase")]
    public void ConferirDocumento_FaseEmBranco_Recusa() =>
        FormatoDosCodigosDoConteudo.ConferirDocumento(Documento("CERTIDAO") with { FaseCodigo = " " }, "documentos[0]")
            .Should().ContainSingle().Which.Field.Should().Be("documentos[0].faseCodigo");

    [Fact(DisplayName = "Os códigos informados do documento não lido: o inválido é recusado e o vazio é pulado")]
    public void ConferirCodigosInformados_PulaOVazioERecusaOInvalido()
    {
        string?[] exceto = [" qzopcao", "OPCAO_VALIDA", string.Empty];

        List<FieldError> erros = [.. FormatoDosCodigosDoConteudo.ConferirCodigosInformados(
            string.Empty, " qztipo", RequisitosAPartirDasFases(" qzprazo", "FASE_VALIDA").TextosDasReferencias("grupos[1].documentos[0]"),
            string.Empty, exceto, "grupos[1].documentos[0]")];

        erros.Select(static e => e.Field).Should().Equal(
            "grupos[1].documentos[0].tipoDocumentoCodigo",
            "grupos[1].documentos[0].requisitos.prazoDeEmissao.referencia.faseCodigo",
            "grupos[1].documentos[0].porOpcao.exceto[0]");
    }

    [Fact(DisplayName = "Os códigos informados válidos, ausentes ou sem requisitos e sem opção não têm recusa")]
    public void ConferirCodigosInformados_SemNadaInvalido_SemRecusa()
    {
        FormatoDosCodigosDoConteudo.ConferirCodigosInformados("INSCRICAO", "CERTIDAO", [], null, null, "documentos[0]").Should().BeEmpty();
        FormatoDosCodigosDoConteudo.ConferirCodigosInformados(
            null, null, RequisitosDoDocumentoDoModelo.AindaNaoEscolhidos.TextosDasReferencias("documentos[0]"), null, null, "documentos[0]")
            .Should().BeEmpty();
    }

    [Fact(DisplayName = "O código de fase da referência em branco é recusado, e o ausente não")]
    public void ConferirCodigosInformados_ReferenciaEmBranco_Recusa() =>
        FormatoDosCodigosDoConteudo.ConferirCodigosInformados(
            "INSCRICAO", "CERTIDAO", [("documentos[0].requisitos.validade.referencia.faseCodigo", " "), ("documentos[0].requisitos.prazoDeEmissao.referencia.faseCodigo", null)],
            null, null, "documentos[0]")
            .Should().ContainSingle().Which.Field.Should().Be("documentos[0].requisitos.validade.referencia.faseCodigo");

    [Fact(DisplayName = "A opção inválida depois de uma nula é recusada na posição que veio")]
    public void ConferirCodigosInformados_OpcaoNulaAntes_RecusaNaPosicaoDoPedido() =>
        FormatoDosCodigosDoConteudo.ConferirCodigosInformados("INSCRICAO", "CERTIDAO", [], "FATO_VALIDO", [null, " qzopcao"], "documentos[0]")
            .Should().ContainSingle().Which.Field.Should().Be("documentos[0].porOpcao.exceto[1]");

    private static List<FieldError> Erros(string codigo) => Conferir(Vazio with { Pressupostos = [codigo] });
    private static List<FieldError> ErrosDoDocumento(string codigo) =>
        Conferir(Vazio with { ConjuntoDeDocumentos = Conjunto(Documento(codigo)) });
    private static List<FieldError> Conferir(ConteudoDoModelo conteudo) =>
        ConferenciaDoConteudoDoModelo.Conferir(DeOutraFaseObrigatoria, conteudo.NaFormaGravada(), new Dictionary<string, IReadOnlyCollection<string>>(), conteudoPorInteiro: true);
}
