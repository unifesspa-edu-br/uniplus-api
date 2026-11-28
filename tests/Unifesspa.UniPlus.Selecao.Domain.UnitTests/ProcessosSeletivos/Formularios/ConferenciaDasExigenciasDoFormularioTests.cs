namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Formularios;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Exigencias;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

/// <summary>
/// A exigência de documento declara o formulário que a entrega (ADR-0171). Ela só é aceita num
/// formulário que existe e é ligado à mesma fase da exigência, obrigatório ou facultativo, e o formulário que ainda
/// tem exigência não pode ser removido.
/// </summary>
public sealed class ConferenciaDasExigenciasDoFormularioTests
{
    private static readonly Guid FaseDaInscricao = Guid.CreateVersion7();
    private static readonly Guid FaseDaHabilitacao = Guid.CreateVersion7();

    private static CodigoDoFormulario Codigo(string valor) => CodigoDoFormulario.Criar(valor).Value!;

    private static FormularioDeclarado Formulario(string codigo, Guid faseId, bool obrigatorio = true) =>
        new(Codigo(codigo), faseId, obrigatorio);

    private static IReadOnlyList<FieldError> Conferir(IEnumerable<FormularioDeclarado> formularios, ExigenciaDeDocumentoDoFormulario[] exigencias) =>
        ConferenciaDasExigenciasDoFormulario.Conferir(formularios, exigencias, CaminhosDasExigencias.NaLista("exigencias", exigencias.Length));

    private static ExigenciaDeDocumentoDoFormulario Exigencia(string codigoDoFormulario, Guid faseId) =>
        new(Codigo(codigoDoFormulario), faseId);

    [Fact(DisplayName = "A exigência no formulário obrigatório da própria fase não tem recusa")]
    public void ExigenciaNoObrigatorioDaMesmaFase_SemRecusa()
    {
        IReadOnlyList<FieldError> recusas = Conferir(
            [Formulario("HABILITACAO", FaseDaHabilitacao)], [Exigencia("HABILITACAO", FaseDaHabilitacao)]);

        recusas.Should().BeEmpty();
    }

    [Fact(DisplayName = "A exigência que cita formulário que o processo não tem é recusada")]
    public void FormularioInexistente_Recusa()
    {
        IReadOnlyList<FieldError> recusas = Conferir(
            [Formulario("INSCRICAO", FaseDaInscricao)], [Exigencia("HABILITACAO", FaseDaHabilitacao)]);

        recusas.Should().ContainSingle().Which.Should().Match<FieldError>(recusa =>
            recusa.Field == "exigencias[0].momento.formularioCodigo" && recusa.Error.Code == FormulariosDeclaradosErrorCodes.ExigenciaEmFormularioInexistente);
    }

    [Fact(DisplayName = "O pedido de isenção, facultativo, exige o comprovante: a exigência em formulário facultativo da mesma fase não tem recusa (UNI-REQ-0144)")]
    public void PedidoDeIsencaoFacultativo_ExigeComprovante_SemRecusa()
    {
        IReadOnlyList<FieldError> recusas = Conferir(
            [Formulario("ISENCAO_TAXA", FaseDaInscricao, obrigatorio: false)], [Exigencia("ISENCAO_TAXA", FaseDaInscricao)]);

        recusas.Should().BeEmpty();
    }

    [Fact(DisplayName = "A exigência de uma fase em formulário de outra fase é recusada")]
    public void FormularioDeOutraFase_Recusa()
    {
        IReadOnlyList<FieldError> recusas = Conferir(
            [Formulario("INSCRICAO", FaseDaInscricao)], [Exigencia("INSCRICAO", FaseDaHabilitacao)]);

        recusas.Should().ContainSingle().Which.Error.Code.Should().Be(FormulariosDeclaradosErrorCodes.ExigenciaEmFormularioDeOutraFase);
    }

    [Fact(DisplayName = "A exigência em formulário facultativo de outra fase é recusada pela fase, não pela obrigatoriedade")]
    public void FormularioFacultativoDeOutraFase_Recusa()
    {
        IReadOnlyList<FieldError> recusas = Conferir(
            [Formulario("ISENCAO_TAXA", FaseDaInscricao, obrigatorio: false)], [Exigencia("ISENCAO_TAXA", FaseDaHabilitacao)]);

        recusas.Should().ContainSingle().Which.Error.Code.Should().Be(FormulariosDeclaradosErrorCodes.ExigenciaEmFormularioDeOutraFase);
    }

    [Fact(DisplayName = "As recusas de cada exigência se acumulam com as das demais, na ordem das exigências")]
    public void Recusas_SeAcumulam()
    {
        IReadOnlyList<FieldError> recusas = Conferir(
            [Formulario("INSCRICAO", FaseDaInscricao)],
            [Exigencia("AUSENTE", FaseDaInscricao), Exigencia("INSCRICAO", FaseDaInscricao), Exigencia("INSCRICAO", FaseDaHabilitacao)]);

        recusas.Select(static recusa => (recusa.Field, recusa.Error.Code)).Should().Equal(
            ("exigencias[0].momento.formularioCodigo", FormulariosDeclaradosErrorCodes.ExigenciaEmFormularioInexistente),
            ("exigencias[2].momento.formularioCodigo", FormulariosDeclaradosErrorCodes.ExigenciaEmFormularioDeOutraFase));
    }

    [Fact(DisplayName = "Com código repetido, a recusa não depende da ordem dos formulários")]
    public void CodigoRepetido_RecusaIndependeDaOrdem()
    {
        FormularioDeclarado daInscricao = Formulario("DADOS", FaseDaInscricao);
        FormularioDeclarado daHabilitacao = Formulario("DADOS", FaseDaHabilitacao);
        ExigenciaDeDocumentoDoFormulario[] exigencias = [Exigencia("DADOS", FaseDaInscricao)];

        Conferir([daInscricao, daHabilitacao], exigencias).Select(static recusa => recusa.Error.Code)
            .Should().Equal(Conferir([daHabilitacao, daInscricao], exigencias).Select(static recusa => recusa.Error.Code))
            .And.Equal(FormulariosDeclaradosErrorCodes.ExigenciaEmFormularioDeOutraFase);
    }

    [Fact(DisplayName = "As mensagens das recusas não repetem o código recusado")]
    public void Mensagens_NaoRepetemOCodigo()
    {
        List<FieldError> recusas =
        [
            .. Conferir([], [Exigencia("AUSENTE", FaseDaInscricao)]),
            .. ConferenciaDasExigenciasDoFormulario.ConferirRemocao([Codigo("AUSENTE")], [Exigencia("AUSENTE", FaseDaInscricao)]),
        ];

        recusas.Should().HaveCount(2).And.OnlyContain(recusa => !recusa.Error.Message.Contains("AUSENTE", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "O formulário removido que ainda tem exigência é recusado, uma vez por formulário")]
    public void RemocaoComExigencia_Recusa()
    {
        IReadOnlyList<FieldError> recusas = ConferenciaDasExigenciasDoFormulario.ConferirRemocao(
            [Codigo("HABILITACAO"), Codigo("INSCRICAO")],
            [Exigencia("HABILITACAO", FaseDaHabilitacao), Exigencia("HABILITACAO", FaseDaHabilitacao)]);

        recusas.Should().ContainSingle().Which.Should().Match<FieldError>(recusa =>
            recusa.Field == "formulariosRemovidos[0]" && recusa.Error.Code == FormulariosDeclaradosErrorCodes.RemocaoDeFormularioComExigencia);
    }

    [Fact(DisplayName = "O formulário removido sem exigência não tem recusa")]
    public void RemocaoSemExigencia_SemRecusa()
    {
        ConferenciaDasExigenciasDoFormulario.ConferirRemocao([Codigo("DADOS_COMPLEMENTARES")], [Exigencia("INSCRICAO", FaseDaInscricao)])
            .Should().BeEmpty();
    }
}
