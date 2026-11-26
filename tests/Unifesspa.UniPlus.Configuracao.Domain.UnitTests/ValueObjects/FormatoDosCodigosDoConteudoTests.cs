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

    private static List<FieldError> Erros(string codigo) => Conferir(Vazio with { Pressupostos = [codigo] });
    private static List<FieldError> ErrosDoDocumento(string codigo) =>
        Conferir(Vazio with { ConjuntoDeDocumentos = Conjunto(Documento(codigo)) });
    private static List<FieldError> Conferir(ConteudoDoModelo conteudo) =>
        ConferenciaDoConteudoDoModelo.Conferir(DeOutraFaseObrigatoria, conteudo.NaFormaGravada(), new Dictionary<string, IReadOnlyCollection<string>>());
}
