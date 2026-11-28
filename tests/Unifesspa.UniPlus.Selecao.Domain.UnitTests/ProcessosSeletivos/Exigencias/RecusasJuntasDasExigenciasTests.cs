namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Exigencias;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDosDocumentosExigidosDoProcessoSeletivo;

/// <summary>
/// Na gravação das exigências, o vínculo com a fase e a etapa, o formulário, a repetição, as datas de referência e o reenvio
/// saem na mesma resposta, sem que uma recusa esconda as dos outros assuntos (ADR-0125, ADR-0173): a exigência de fase fora
/// do cronograma recebe a recusa da fase e as que independem dela.
/// </summary>
public sealed class RecusasJuntasDasExigenciasTests
{
    private const string FaseFora = "DocumentoExigido.FaseNaoPertenceAoProcesso";
    private const string ReenvioSemComplementacao = "DocumentoExigido.PendenciaReenvioExigeComplementacao";
    private const string FormularioInexistente = FormulariosDeclaradosErrorCodes.ExigenciaEmFormularioInexistente;
    private const string FormularioDeOutraFase = FormulariosDeclaradosErrorCodes.ExigenciaEmFormularioDeOutraFase;

    private static (ProcessoSeletivo Processo, Guid Inscricao, Guid Habilitacao) ProcessoComOsDoisFormularios()
    {
        FaseCronograma inscricao = FaseQueResponde(1, "INSCRICAO", inscricao: true, isencao: false);
        FaseCronograma habilitacao = Fase(2, "HABILITACAO");
        ProcessoSeletivo processo = ProcessoComFases(inscricao, habilitacao);
        FormularioDeTeste.GarantirFormulario(processo, PapelDoFormularioDeTeste.Inscricao);
        processo.DefinirFormulario(
                FormularioDeTeste.DeclaradoNoPapel(PapelDoFormularioDeTeste.Habilitacao, habilitacao.Id), null, FormularioDeTeste.Paginas(PapelDoFormularioDeTeste.Habilitacao))
            .IsSuccess.Should().BeTrue();

        return (processo, inscricao.Id, habilitacao.Id);
    }

    private static RegraDeComprovacao Regra(Guid fase, int ordem, PapelDoFormularioDeTeste papel = PapelDoFormularioDeTeste.Inscricao, bool abrePrazoParaReenvio = false) =>
        RegraDeComprovacao.CriarRegraDeDocumento(Exigencia(fase, papel: papel, abrePrazoParaReenvio: abrePrazoParaReenvio), ordem).Value!;

    private static RegraDeComprovacao RegraComPrazoReferenciadoEmFaseFora(Guid fase, int ordem)
    {
        DataDeReferenciaDoDocumento referencia = new DataDeReferenciaBuilder().NoInicioDaFase(Guid.CreateVersion7()).Construir();
        DocumentoExigido documento = new DocumentoExigidoBuilder().NaFase(fase)
            .ComPrazoDeEmissao(new PrazoDeEmissaoBuilder().Referenciado(referencia).Construir())
            .ComValidade(new ValidadeDoDocumentoBuilder().NaoExige().Construir())
            .Construir();
        return RegraDeComprovacao.CriarRegraDeDocumento(documento, ordem).Value!;
    }

    private static IEnumerable<(string? Campo, string Codigo)> Recusas(Result resultado) =>
        resultado.Errors.Select(static erro => (erro.Field, erro.Error.Code));

    [Fact(DisplayName = "A fase fora do cronograma de uma exigência e o formulário inexistente de outra saem juntos, cada um no campo da sua exigência")]
    public void FaseForaEFormularioInexistenteEmExigenciasDiferentes_SaemJuntos()
    {
        FaseCronograma inscricao = FaseQueResponde(1, "INSCRICAO", inscricao: true, isencao: false);
        ProcessoSeletivo processo = ProcessoComFases(inscricao);
        FormularioDeTeste.GarantirFormulario(processo, PapelDoFormularioDeTeste.Inscricao);

        Result resultado = processo.DefinirDocumentosExigidos(
            [Regra(Guid.CreateVersion7(), 0), Regra(inscricao.Id, 1, PapelDoFormularioDeTeste.Habilitacao)]);

        Recusas(resultado).Should().Equal(
            ("regrasDeComprovacao[0].documento.momento.faseId", FaseFora),
            ("regrasDeComprovacao[1].documento.momento.formularioCodigo", FormularioInexistente));
        processo.DocumentosExigidos.Should().BeEmpty();
    }

    [Fact(DisplayName = "As fases fora do cronograma de duas exigências saem juntas")]
    public void FaseForaEmDuasExigencias_SaemJuntas()
    {
        (ProcessoSeletivo processo, _, _) = ProcessoComOsDoisFormularios();

        Result resultado = processo.DefinirDocumentosExigidos([Regra(Guid.CreateVersion7(), 0), Regra(Guid.CreateVersion7(), 1)]);

        Recusas(resultado).Should().Equal(
            ("regrasDeComprovacao[0].documento.momento.faseId", FaseFora),
            ("regrasDeComprovacao[1].documento.momento.faseId", FaseFora));
    }

    [Fact(DisplayName = "Só a fase fora do cronograma recusa a exigência cujo formulário o processo tem")]
    public void SoFaseFora_RecusaSoAFase()
    {
        (ProcessoSeletivo processo, _, _) = ProcessoComOsDoisFormularios();

        Result resultado = processo.DefinirDocumentosExigidos([Regra(Guid.CreateVersion7(), 0)]);

        Recusas(resultado).Should().Equal(("regrasDeComprovacao[0].documento.momento.faseId", FaseFora));
    }

    [Fact(DisplayName = "A exigência de fase fora do cronograma não recebe a recusa de formulário de outra fase, e a de fase do cronograma recebe")]
    public void FaseForaComFormularioDeOutraFase_RecebeSoAFase()
    {
        (ProcessoSeletivo processo, Guid inscricao, _) = ProcessoComOsDoisFormularios();

        Result resultado = processo.DefinirDocumentosExigidos(
            [Regra(Guid.CreateVersion7(), 0, PapelDoFormularioDeTeste.Habilitacao), Regra(inscricao, 1, PapelDoFormularioDeTeste.Habilitacao)]);

        Recusas(resultado).Should().Equal(
            ("regrasDeComprovacao[0].documento.momento.faseId", FaseFora),
            ("regrasDeComprovacao[1].documento.momento.formularioCodigo", FormularioDeOutraFase));
    }

    [Fact(DisplayName = "A fase fora do cronograma e o formulário inexistente da mesma exigência saem juntos")]
    public void FaseForaComFormularioInexistente_SaemAsDuas()
    {
        FaseCronograma inscricao = FaseQueResponde(1, "INSCRICAO", inscricao: true, isencao: false);
        ProcessoSeletivo processo = ProcessoComFases(inscricao);
        FormularioDeTeste.GarantirFormulario(processo, PapelDoFormularioDeTeste.Inscricao);

        Result resultado = processo.DefinirDocumentosExigidos([Regra(Guid.CreateVersion7(), 0, PapelDoFormularioDeTeste.Habilitacao)]);

        Recusas(resultado).Should().Equal(
            ("regrasDeComprovacao[0].documento.momento.faseId", FaseFora),
            ("regrasDeComprovacao[0].documento.momento.formularioCodigo", FormularioInexistente));
    }

    [Fact(DisplayName = "O assunto vem antes da posição: a fase fora do cronograma da segunda exigência sai antes do reenvio sem complementação da primeira")]
    public void AssuntoAntesDaPosicao_FaseForaDaSegundaAntesDoReenvioDaPrimeira()
    {
        FaseCronograma inscricao = FaseQueResponde(1, "INSCRICAO", inscricao: true, isencao: false);
        ProcessoSeletivo processo = ProcessoComFases(inscricao);
        FormularioDeTeste.GarantirFormulario(processo, PapelDoFormularioDeTeste.Inscricao);

        Result resultado = processo.DefinirDocumentosExigidos([Regra(inscricao.Id, 0, abrePrazoParaReenvio: true), Regra(Guid.CreateVersion7(), 1)]);

        Recusas(resultado).Should().Equal(
            ("regrasDeComprovacao[1].documento.momento.faseId", FaseFora),
            ("regrasDeComprovacao[0].documento.indeferimento.abrePrazoParaReenvio", ReenvioSemComplementacao));
    }

    [Fact(DisplayName = "A fase de referência fora do cronograma sai junto da fase fora do cronograma da mesma exigência")]
    public void DatasDeReferenciaJuntoDaFaseFora()
    {
        (ProcessoSeletivo processo, _, _) = ProcessoComOsDoisFormularios();

        Result resultado = processo.DefinirDocumentosExigidos([RegraComPrazoReferenciadoEmFaseFora(Guid.CreateVersion7(), 0)]);

        Recusas(resultado).Should().Equal(
            ("regrasDeComprovacao[0].documento.momento.faseId", FaseFora),
            ("regrasDeComprovacao[0].documento.requisitos.prazoDeEmissao.referencia", DataDeReferenciaDoDocumentoErrorCodes.FaseNaoPertenceAoProcesso));
    }

    [Fact(DisplayName = "As exigências de fase do cronograma, com o formulário da fase, são aceitas")]
    public void ExigenciasValidas_SaoAceitas()
    {
        (ProcessoSeletivo processo, Guid inscricao, Guid habilitacao) = ProcessoComOsDoisFormularios();

        Result resultado = processo.DefinirDocumentosExigidos([Regra(inscricao, 0), Regra(habilitacao, 1, PapelDoFormularioDeTeste.Habilitacao)]);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.DocumentosExigidos.Should().HaveCount(2);
    }
}
