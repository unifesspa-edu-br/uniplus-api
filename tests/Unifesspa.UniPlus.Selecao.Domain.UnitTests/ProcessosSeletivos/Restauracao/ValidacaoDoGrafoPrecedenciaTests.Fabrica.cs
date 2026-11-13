namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Restauracao;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Desempate;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ProcessoSeletivoRestaurarConfiguracaoTests;

public sealed partial class ValidacaoDoGrafoPrecedenciaTests
{
    private const string FaseDaBase = "FASE_DA_BASE";
    private const string FaseForaDoCronograma = "FASE_FORA_DO_CRONOGRAMA";

    private static Guid Id(int numero) => new($"00000000-0000-4000-8000-{numero:D12}");

    private static EtapaProcesso EtapaDoEnemSemPeso(Guid id, int ordem) =>
        EtapaProcesso.Reidratar(id, "Nota do ENEM", CaraterEtapa.Eliminatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "NOTA_ENEM", "Nota do ENEM", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: true).Value!, null, null, ordem);

    // EtapaProcesso.Reidratar recusa o Id vazio; a recusa do grafo vale para a etapa que o perde depois de reidratada.
    private static EtapaProcesso EtapaSemId(int ordem)
    {
        EtapaProcesso etapa = EtapaDaProvaCongelada(Guid.CreateVersion7(), ordem);
        typeof(EntityBase).GetProperty(nameof(EntityBase.Id))!.SetValue(etapa, Guid.Empty);
        return etapa;
    }

    private static EtapaProcesso NaFase(EtapaProcesso etapa, string? faseCodigo) =>
        EtapaProcesso.Reidratar(etapa.Id, etapa.Nome, etapa.Carater, etapa.TipoEtapa, etapa.Peso, etapa.NotaMinima, etapa.Ordem, faseCodigo);

    private static CriterioDesempate DesempatePorIdade(int ordem) =>
        CriterioDesempate.Criar(ordem, Regra(CriterioDesempateCodigo.MaiorIdade, 'f'), new ArgsDesempateMaiorIdade()).Value!;

    private static CriterioDesempate DesempatePelaNotaDaEtapa(int ordem, Guid etapaRef) =>
        CriterioDesempate.Criar(ordem, Regra(CriterioDesempateCodigo.MaiorNotaEtapa, 'd'), new ArgsDesempateMaiorNotaEtapa(etapaRef)).Value!;

    private static RegraEliminacao EliminacaoPorAusenciaNaEtapa(Guid etapaRef) =>
        RegraEliminacao.Criar(Regra(RegraEliminacaoCodigo.ElimAusenciaNaEtapa, 'e'), new ArgsElimAusenciaNaEtapa(etapaRef)).Value!;

    private static FatoColetado FatoDoFormulario(int indice) =>
        FatoColetado.Criar(
            $"FATO_{indice}", indice, "Campo", TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, null, finalidade: FinalidadeFormulario.Inscricao, classificacaoProtecao: "PESSOAL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;

    private static FaseCronograma Fase(int ordem, Guid faseCanonicaOrigemId, bool agrupaEtapas, string? codigo = null) => FaseCronograma.Reidratar(
        Guid.CreateVersion7(), ordem, faseCanonicaOrigemId, codigo ?? $"FASE_{ordem}",
        origemData: OrigemDataFase.NoEdital, agrupaEtapas: agrupaEtapas,
        permiteComplementacao: false, coletaInscricao: false, coletaSolicitacaoIsencao: false,
        inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
        produtos: [ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)],
        faseConcluinteCodigo: null,
        emiteParecerIndividual: false,
        bancasRequeridas: [], regraRecurso: null, unidadeResponsavel: UnidadesDeTeste.Ceps);

    private static NoExigencia FolhaDeDocumentoAusenteDoGrafo()
    {
        DocumentoExigido documento = DocumentoQualquer(Guid.CreateVersion7());
        return NoExigencia.Reidratar(
            Guid.CreateVersion7(), TipoNo.Folha, 0, documento.Id, documento, 1, null, null, null, null, null, [], []);
    }
}
