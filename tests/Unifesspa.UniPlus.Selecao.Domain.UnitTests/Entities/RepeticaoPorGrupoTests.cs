namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
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
        FaseCronograma.Criar(
            ordem, Guid.CreateVersion7(), codigo, "CEPS", OrigemDataFase.Propria,
            agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: coletaInscricao, coletaSolicitacaoIsencao: coletaIsencao,
            inicio: new DateTimeOffset(2026, 1, ordem, 0, 0, 0, TimeSpan.Zero), fim: new DateTimeOffset(2026, 1, ordem + 1, 0, 0, 0, TimeSpan.Zero),
            produtos: [ProdutoDaFase.Criar(codigo, PapelProdutoFase.Definitivo)],
            faseConcluinteCodigo: null, emiteParecerIndividual: false, bancasRequeridas: [], regraRecurso: null).Value!;

    private static GrupoColetado GrupoDaComposicao() => GrupoColetado.Criar(
        Composicao, 0, FormularioDeTeste.Secao, "Composição familiar", 1, 10, null, Obrigatoriedade.Sempre,
        [FatoColetado.Criar("SOB_GUARDA", 0, "Sob guarda", TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, null, classificacaoProtecao: "PESSOAL").Value!]).Value!;

    private static DocumentoExigido Documento(Guid faseId, FinalidadeFormulario finalidade) => DocumentoExigido.Criar(
        faseId, Guid.CreateVersion7(), "CERTIDAO_GUARDA", "Certidão de guarda", "CAT", Aplicabilidade.Geral,
        obrigatorio: true, consequenciaIndeferimento: null, [], [], null, Qualquer, null, finalidade: finalidade).Value!;

    /// <summary>Processo com inscrição e habilitação, e o grupo da composição familiar no formulário da habilitação.</summary>
    private static (ProcessoSeletivo Processo, FaseCronograma Inscricao, FaseCronograma Habilitacao) Processo()
    {
        FaseCronograma inscricao = Fase(1, "INSCRICAO", coletaInscricao: true);
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar(fase: inscricao);
        FaseCronograma habilitacao = Fase(2, FormularioProcesso.CodigoFaseHabilitacao);
        processo.DefinirCronogramaFases([inscricao, habilitacao], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirFormulario(FinalidadeFormulario.Habilitacao, habilitacao.Id, null, FormularioDeTeste.Etapas(), PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        processo.DefinirFatosColetados(FinalidadeFormulario.Habilitacao, [], PrecondicaoIfMatch.Ausente, [GrupoDaComposicao()])
            .IsSuccess.Should().BeTrue();
        return (processo, inscricao, habilitacao);
    }

    [Fact(DisplayName = "Repetir por grupo que o processo não tem é recusado")]
    public void DefinirDocumentosExigidos_GrupoInexistente_Recusa()
    {
        (ProcessoSeletivo processo, _, FaseCronograma habilitacao) = Processo();

        Result resultado = processo.DefinirDocumentosExigidos(
            [NoExigencia.CriarFolha(Documento(habilitacao.Id, FinalidadeFormulario.Habilitacao), 0, repetePorEntidade: "PESSOAS_JURIDICAS").Value!], PrecondicaoIfMatch.Ausente);

        resultado.Error!.Code.Should().Be("NoExigencia.TipoEntidadeInvalido");
    }

    [Fact(DisplayName = "Repetir na inscrição pelo grupo coletado só na habilitação é recusado")]
    public void DefinirDocumentosExigidos_GrupoDeFasePosterior_Recusa()
    {
        (ProcessoSeletivo processo, FaseCronograma inscricao, FaseCronograma habilitacao) = Processo();

        processo.DefinirDocumentosExigidos(
                [NoExigencia.CriarFolha(Documento(inscricao.Id, FinalidadeFormulario.Inscricao), 0, repetePorEntidade: Composicao).Value!], PrecondicaoIfMatch.Ausente)
            .Error!.Code.Should().Be(DocumentoExigidoErrorCodes.FatoResolvidoEmFasePosterior);
        processo.DefinirDocumentosExigidos(
                [NoExigencia.CriarFolha(Documento(habilitacao.Id, FinalidadeFormulario.Habilitacao), 0, repetePorEntidade: Composicao).Value!], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
    }

    [Theory(DisplayName = "Na fase que divide inscrição e isenção, repetir pelo grupo coletado só na isenção vale só no documento da isenção")]
    [InlineData(FinalidadeFormulario.IsencaoTaxa, true)]
    [InlineData(FinalidadeFormulario.Inscricao, false)]
    public void DefinirDocumentosExigidos_GrupoDaIsencaoNaFaseCompartilhada_SegueAFinalidade(FinalidadeFormulario finalidade, bool aceito)
    {
        FaseCronograma compartilhada = Fase(1, "INSCRICAO", coletaInscricao: true, coletaIsencao: true);
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar(fase: compartilhada);
        processo.DefinirCronogramaFases([compartilhada], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirFormulario(FinalidadeFormulario.IsencaoTaxa, compartilhada.Id, null, FormularioDeTeste.Etapas(), PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        processo.DefinirFatosColetados(FinalidadeFormulario.IsencaoTaxa, [], PrecondicaoIfMatch.Ausente, [GrupoDaComposicao()])
            .IsSuccess.Should().BeTrue();

        Result resultado = processo.DefinirDocumentosExigidos(
            [NoExigencia.CriarFolha(Documento(compartilhada.Id, finalidade), 0, repetePorEntidade: Composicao).Value!], PrecondicaoIfMatch.Ausente);

        resultado.IsSuccess.Should().Be(aceito, resultado.Error?.Message);
        if (!aceito)
        {
            resultado.Error!.Code.Should().Be(DocumentoExigidoErrorCodes.FatoDaIsencaoEmOutraFinalidade);
        }
    }

    [Fact(DisplayName = "Na publicação, a repetição pelo grupo retirado do formulário depois é recusada")]
    public void PendenciaPreCanonicalizacao_GrupoRetiradoDepois_Recusa()
    {
        (ProcessoSeletivo processo, _, FaseCronograma habilitacao) = Processo();
        processo.DefinirDocumentosExigidos(
                [NoExigencia.CriarFolha(Documento(habilitacao.Id, FinalidadeFormulario.Habilitacao), 0, repetePorEntidade: Composicao).Value!], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        processo.DefinirFatosColetados(FinalidadeFormulario.Habilitacao, [], PrecondicaoIfMatch.Ausente, []).IsSuccess.Should().BeTrue();

        processo.PendenciaPreCanonicalizacao(FatosDeModalidadeDeTeste.DoCatalogo)!.Code.Should().Be("NoExigencia.TipoEntidadeInvalido");
    }

    [Fact(DisplayName = "A folha dentro da subárvore repetida cita os campos do grupo; a de fora, não")]
    public void CamposDaRepeticao_PelaSubarvoreRepetida()
    {
        (ProcessoSeletivo processo, _, FaseCronograma habilitacao) = Processo();
        DocumentoExigido certidao = Documento(habilitacao.Id, FinalidadeFormulario.Habilitacao);
        DocumentoExigido avulso = Documento(habilitacao.Id, FinalidadeFormulario.Habilitacao);
        NoExigencia repetido = NoExigencia.CriarGrupo(
            TipoNo.GrupoE, 0, null, null, [], [NoExigencia.CriarFolha(certidao, 0).Value!], repetePorEntidade: Composicao).Value!;
        processo.DefinirDocumentosExigidos([repetido, NoExigencia.CriarFolha(avulso, 1).Value!], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        processo.CamposDaRepeticao(certidao.Id).Should().BeEquivalentTo(["SOB_GUARDA"]);
        processo.CamposDaRepeticao(avulso.Id).Should().BeEmpty();
    }
}
