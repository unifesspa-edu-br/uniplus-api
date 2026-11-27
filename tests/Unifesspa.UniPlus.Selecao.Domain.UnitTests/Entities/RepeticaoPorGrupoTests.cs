namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Exigencias;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A exigência repetida por entidade repete pelas ocorrências de um grupo repetível do formulário
/// do processo (ADR-0138, UNI-REQ-0069): o grupo existe e é conhecido até a fase da exigência, na
/// definição e de novo na publicação, porque os grupos mudam depois.
/// </summary>
public sealed class RepeticaoPorGrupoTests
{
    private const string Composicao = "COMPOSICAO_FAMILIAR";

    private static readonly FormatosPermitidos Qualquer = FormatosPermitidos.Criar(true, null).Value!;

    private static FaseCronograma Fase(int ordem, string codigo, bool coletaInscricao = false, bool coletaIsencao = false) =>
        FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
            OrigensDoTipoDeFaseDeTeste.Da(
                Guid.CreateVersion7(), codigo, OrigemDataFase.NoEdital, agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: coletaInscricao, coletaSolicitacaoIsencao: coletaIsencao),
            new DecisoesDeCronogramaDaFase(
                ordem, Inicio: new DateTimeOffset(2026, 1, ordem, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 1, ordem + 1, 0, 0, 0, TimeSpan.Zero), UnidadeResponsavel: UnidadesDeTeste.Ceps),
            new PublicacoesDaFase(Produtos: [ProdutoDaFase.Criar(codigo, PapelDaPublicacaoPrevista.Definitivo)], EmiteParecerIndividual: false, RegraRecurso: null),
            bancasRequeridas: []).Value!;

    private static GrupoColetado GrupoDaComposicao() => GrupoColetadoDeTeste.Agrupando(Composicao, 0)
        .ComOcorrencias(1, 10)
        .ComObrigatoriedade(Obrigatoriedade.Sempre)
        .ComCampos(FatoColetadoDeTeste.Coletando("SOB_GUARDA", 0).ComRotulo("Sob guarda").ComObrigatoriedade(Obrigatoriedade.Sempre).Construir())
        .Construir();

    private static DocumentoExigido Documento(Guid faseId, PapelDoFormularioDeTeste papel) => new Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios.DocumentoExigidoBuilder()
            .ComTipo(new CopiaDoTipoDeDocumento(Guid.CreateVersion7(), "CERTIDAO_GUARDA", "Certidão de guarda", "CAT", ProtecaoDoDocumentoDeTeste.Pessoal))
            .ComMomento(new MomentoDaExigencia(faseId, null, FormularioDeTeste.CodigoDoPapel(papel)))
            .ComAplicabilidade(new AplicabilidadeDaExigencia(Aplicabilidade.Geral, []))
            .ComIndeferimento(new IndeferimentoDoDocumento(true, null, [], false))
            .ComRequisitos(new RequisitosDoDocumento(Qualquer, null, null, null, null))
            .ComFatos(null)
            .Criar().Value!;

    /// <summary>Processo com inscrição e habilitação, e o grupo da composição familiar no formulário da habilitação.</summary>
    private static (ProcessoSeletivo Processo, FaseCronograma Inscricao, FaseCronograma Habilitacao) Processo()
    {
        FaseCronograma inscricao = Fase(1, "INSCRICAO", coletaInscricao: true);
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar(fase: inscricao);
        FaseCronograma habilitacao = Fase(2, "HABILITACAO");
        processo.DefinirCronogramaFases([inscricao, habilitacao], []).IsSuccess.Should().BeTrue();
        processo.DefinirFormulario(PapelDoFormularioDeTeste.Habilitacao, habilitacao.Id, null, FormularioDeTeste.Paginas())
            .IsSuccess.Should().BeTrue();
        processo.DefinirFatosColetados(PapelDoFormularioDeTeste.Habilitacao, [], [GrupoDaComposicao()])
            .IsSuccess.Should().BeTrue();
        return (processo, inscricao, habilitacao);
    }

    /// <summary>Processo com inscrição e isenção na mesma fase, e o grupo da composição familiar no formulário da isenção.</summary>
    private static (ProcessoSeletivo Processo, FaseCronograma Compartilhada) ProcessoComGrupoDaIsencaoNaFaseCompartilhada()
    {
        FaseCronograma compartilhada = Fase(1, "INSCRICAO", coletaInscricao: true, coletaIsencao: true);
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar(fase: compartilhada);
        processo.DefinirCronogramaFases([compartilhada], []).IsSuccess.Should().BeTrue();
        processo.DefinirFormulario(PapelDoFormularioDeTeste.IsencaoTaxa, compartilhada.Id, null, FormularioDeTeste.Paginas())
            .IsSuccess.Should().BeTrue();
        processo.DefinirFatosColetados(PapelDoFormularioDeTeste.IsencaoTaxa, [], [GrupoDaComposicao()])
            .IsSuccess.Should().BeTrue();

        return (processo, compartilhada);
    }

    [Fact(DisplayName = "Repetir por grupo que o processo não tem é recusado")]
    public void DefinirDocumentosExigidos_GrupoInexistente_Recusa()
    {
        (ProcessoSeletivo processo, _, FaseCronograma habilitacao) = Processo();

        Result resultado = processo.DefinirDocumentosExigidos(
            [RegraDeComprovacao.CriarRegraDeDocumento(Documento(habilitacao.Id, PapelDoFormularioDeTeste.Habilitacao), 0, grupoDoFormularioParaRepeticao: "PESSOAS_JURIDICAS").Value!]);

        resultado.Error!.Code.Should().Be("RegraDeComprovacao.GrupoParaRepeticaoNaoDeclarado");
    }

    [Fact(DisplayName = "Repetir na inscrição pelo grupo coletado só na habilitação é recusado")]
    public void DefinirDocumentosExigidos_GrupoDeFasePosterior_Recusa()
    {
        (ProcessoSeletivo processo, FaseCronograma inscricao, FaseCronograma habilitacao) = Processo();

        processo.DefinirDocumentosExigidos(
                [RegraDeComprovacao.CriarRegraDeDocumento(Documento(inscricao.Id, PapelDoFormularioDeTeste.Inscricao), 0, grupoDoFormularioParaRepeticao: Composicao).Value!])
            .Error!.Code.Should().Be(DocumentoExigidoErrorCodes.FatoResolvidoEmFasePosterior);
        processo.DefinirDocumentosExigidos(
                [RegraDeComprovacao.CriarRegraDeDocumento(Documento(habilitacao.Id, PapelDoFormularioDeTeste.Habilitacao), 0, grupoDoFormularioParaRepeticao: Composicao).Value!])
            .IsSuccess.Should().BeTrue();
    }

    [Theory(DisplayName = "Na fase que divide inscrição e isenção, repetir pelo grupo coletado só na isenção vale só no documento da isenção")]
    [InlineData(PapelDoFormularioDeTeste.IsencaoTaxa, true)]
    [InlineData(PapelDoFormularioDeTeste.Inscricao, false)]
    public void DefinirDocumentosExigidos_GrupoDaIsencaoNaFaseCompartilhada_SegueOFormulario(PapelDoFormularioDeTeste papel, bool aceito)
    {
        (ProcessoSeletivo processo, FaseCronograma compartilhada) = ProcessoComGrupoDaIsencaoNaFaseCompartilhada();

        Result resultado = processo.DefinirDocumentosExigidos(
            [RegraDeComprovacao.CriarRegraDeDocumento(Documento(compartilhada.Id, papel), 0, grupoDoFormularioParaRepeticao: Composicao).Value!]);

        resultado.IsSuccess.Should().Be(aceito, resultado.Error?.Message);
        if (!aceito)
        {
            resultado.Error!.Code.Should().Be(DocumentoExigidoErrorCodes.FatoDeFormularioNaoAlcancado);
        }
    }

    [Fact(DisplayName = "Na publicação, a repetição pelo grupo retirado do formulário depois é recusada")]
    public void PendenciaPreCanonicalizacao_GrupoRetiradoDepois_Recusa()
    {
        (ProcessoSeletivo processo, _, FaseCronograma habilitacao) = Processo();
        processo.DefinirDocumentosExigidos(
                [RegraDeComprovacao.CriarRegraDeDocumento(Documento(habilitacao.Id, PapelDoFormularioDeTeste.Habilitacao), 0, grupoDoFormularioParaRepeticao: Composicao).Value!])
            .IsSuccess.Should().BeTrue();

        processo.DefinirFatosColetados(PapelDoFormularioDeTeste.Habilitacao, [], []).IsSuccess.Should().BeTrue();

        CondicoesParaRegistrarConfiguracaoDeTeste.PrimeiraCausa(processo)!.Code.Should().Be("RegraDeComprovacao.GrupoParaRepeticaoNaoDeclarado");
    }

    [Fact(DisplayName = "A regra repetida cita os campos do grupo; a regra de fora, não")]
    public void CamposDaRepeticao_PelasRegrasRepetidas()
    {
        (ProcessoSeletivo processo, _, FaseCronograma habilitacao) = Processo();
        DocumentoExigido certidao = Documento(habilitacao.Id, PapelDoFormularioDeTeste.Habilitacao);
        DocumentoExigido avulso = Documento(habilitacao.Id, PapelDoFormularioDeTeste.Habilitacao);
        RegraDeComprovacao repetido = RegraDeComprovacao.CriarGrupo(
            new(FormaDeComprovacao.TodosOsItens, null, null), 0, [], [RegraDeComprovacao.CriarRegraDeDocumento(certidao, 0).Value!], grupoDoFormularioParaRepeticao: Composicao).Value!;
        processo.DefinirDocumentosExigidos([repetido, RegraDeComprovacao.CriarRegraDeDocumento(avulso, 1).Value!])
            .IsSuccess.Should().BeTrue();

        RegrasDasExigencias.CamposDaRepeticao(processo.Declaracao(), certidao.Id).Should().BeEquivalentTo(["SOB_GUARDA"]);
        RegrasDasExigencias.CamposDaRepeticao(processo.Declaracao(), avulso.Id).Should().BeEmpty();
    }
}
