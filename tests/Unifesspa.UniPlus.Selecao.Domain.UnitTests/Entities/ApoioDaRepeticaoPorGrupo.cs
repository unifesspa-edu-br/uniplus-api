namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Montagem comum dos testes da repetição por grupo: o processo com inscrição e habilitação, o grupo da composição
/// familiar no formulário da habilitação e a exigência que o cita.
/// </summary>
internal static class ApoioDaRepeticaoPorGrupo
{
    internal const string Composicao = "COMPOSICAO_FAMILIAR";

    private static readonly FormatosPermitidos Qualquer = FormatosPermitidos.Criar(true, null).Value!;

    internal static FaseCronograma Fase(int ordem, string codigo, bool coletaInscricao = false, bool coletaIsencao = false) =>
        FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
            OrigensDoTipoDeFaseDeTeste.Da(
                Guid.CreateVersion7(), codigo, OrigemDataFase.NoEdital, agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: coletaInscricao, coletaSolicitacaoIsencao: coletaIsencao),
            new DecisoesDeCronogramaDaFase(
                ordem, Inicio: new DateTimeOffset(2026, 1, ordem, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 1, ordem + 1, 0, 0, 0, TimeSpan.Zero), UnidadeResponsavel: UnidadesDeTeste.Ceps),
            new PublicacoesDaFase(Produtos: [ProdutoDaFase.Criar(codigo, PapelDaPublicacaoPrevista.Definitivo)], EmiteParecerIndividual: false, RegraRecurso: null),
            bancasRequeridas: []).Value!;

    internal static GrupoColetado GrupoDaComposicao() => GrupoColetadoDeTeste.Agrupando(Composicao, 0)
        .ComOcorrencias(1, 10)
        .ComObrigatoriedade(Obrigatoriedade.Sempre)
        .ComCampos(FatoColetadoDeTeste.Coletando("SOB_GUARDA", 0).ComRotulo("Sob guarda").ComObrigatoriedade(Obrigatoriedade.Sempre).Construir())
        .Construir();

    internal static DocumentoExigido Documento(Guid faseId, PapelDoFormularioDeTeste papel) => new Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios.DocumentoExigidoBuilder()
            .ComTipo(new CopiaDoTipoDeDocumento(Guid.CreateVersion7(), "CERTIDAO_GUARDA", "Certidão de guarda", "CAT", ProtecaoDoDocumentoDeTeste.Pessoal))
            .ComMomento(new MomentoDaExigencia(faseId, null, FormularioDeTeste.CodigoDoPapel(papel)))
            .ComAplicabilidade(new AplicabilidadeDaExigencia(Aplicabilidade.Geral, []))
            .ComIndeferimento(new IndeferimentoDoDocumento(true, null, [], false))
            .ComRequisitos(new RequisitosDoDocumento(Qualquer, null, null, null, null))
            .ComFatos(null)
            .Criar().Value!;

    /// <summary>Processo com inscrição e habilitação, e o grupo da composição familiar no formulário da habilitação.</summary>
    internal static (ProcessoSeletivo Processo, FaseCronograma Inscricao, FaseCronograma Habilitacao) Processo()
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
}
