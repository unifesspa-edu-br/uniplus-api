namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;

using System.Reflection;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed partial class PendenciasDoCronogramaPrecedenciaTests
{
    private const string FaseDaBase = "FASE_DA_BASE";
    private const string CodigoDaFaseComJanela = "FASE_COM_JANELA";

    private static readonly TimeZoneInfo Belem = TimeZoneInfo.FindSystemTimeZoneById(FusoInstitucional.ZoneId);
    private static readonly DateTimeOffset AberturaDasInscricoes = InstanteEmBelem.Em(2026, 3, 2);
    private static readonly DateTimeOffset FimDasInscricoes = InstanteEmBelem.Em(2026, 3, 31, 23, 59, 59);

    /// <summary>
    /// O cronograma conforme, com o que cada pendência acrescenta sobre ele. As pendências são aditivas,
    /// cada uma em fases e etapas próprias, para que um mesmo estado viole várias ao mesmo tempo.
    /// </summary>
    private sealed class CronogramaEmMontagem
    {
        public OrigemCandidatos Origem { get; set; } = OrigemCandidatos.ImportacaoExterna;

        public bool ComVagas { get; set; }

        public List<FaseCronograma> Fases { get; } = [new ModeloDaFase(1, FaseDaBase).Reidratar()];

        public List<EtapaProcesso> Etapas { get; } = [];

        public int ProximaOrdem => Fases.Count + 1;

        public static CronogramaEmMontagem Com(params Pendencia[] pendencias)
        {
            CronogramaEmMontagem cronograma = new();
            foreach (Pendencia pendencia in pendencias)
            {
                Recusas[pendencia].Provoca(cronograma);
            }

            return cronograma;
        }

        public EstadoDaConfiguracao Montar() => new EstadoDaConfiguracaoBuilder().Construir() with
        {
            OrigemCandidatos = Origem,
            CronogramaFases = Fases,
            Etapas = Etapas,
            DistribuicaoVagas = ComVagas ? [ProcessoConformeFactory.Oferta()] : [],
        };
    }

    private sealed record ModeloDaFase(int Ordem, string Codigo)
    {
        public bool AgrupaEtapas { get; init; }

        public bool ColetaInscricao { get; init; }

        public bool ColetaSolicitacaoIsencao { get; init; }

        public DateTimeOffset? Inicio { get; init; }

        public DateTimeOffset? Fim { get; init; }

        public IReadOnlyList<ProdutoDaFase> Produtos { get; init; } = [];

        public string? FaseConcluinteCodigo { get; init; }

        public RegraRecursoFase? RegraRecurso { get; init; }

        public UnidadeResponsavelSnapshot? Unidade { get; init; } = UnidadesDeTeste.Ceps;

        public FaseCronograma Reidratar() => FaseCronograma.Reidratar(
            Guid.CreateVersion7(), Ordem, Guid.CreateVersion7(), Codigo, OrigemDataFase.NaExecucao,
            AgrupaEtapas, permiteComplementacao: false, ColetaInscricao, ColetaSolicitacaoIsencao, Inicio, Fim,
            Produtos, FaseConcluinteCodigo, emiteParecerIndividual: false, bancasRequeridas: [], RegraRecurso, Unidade);
    }

    private static FaseCronograma FaseDeAvaliacao(int ordem) =>
        new ModeloDaFase(ordem, "FASE_DE_AVALIACAO") { AgrupaEtapas = true }.Reidratar();

    private static FaseCronograma FaseDeInscricao(int ordem, DateTimeOffset? inicio, DateTimeOffset? fim) =>
        new ModeloDaFase(ordem, "INSCRICAO") { ColetaInscricao = true, Inicio = inicio, Fim = fim }.Reidratar();

    private static FaseCronograma FaseDeIsencao(int ordem, DateTimeOffset? inicio, DateTimeOffset? fim) =>
        new ModeloDaFase(ordem, "SOLICITACAO_ISENCAO") { ColetaSolicitacaoIsencao = true, Inicio = inicio, Fim = fim }.Reidratar();

    private static FaseCronograma FaseSemUnidade(int ordem) =>
        new ModeloDaFase(ordem, "FASE_SEM_UNIDADE") { Unidade = null }.Reidratar();

    private static FaseCronograma FaseComConclusaoSemPreliminar(int ordem) =>
        new ModeloDaFase(ordem, "FASE_COM_CONCLUSAO") { FaseConcluinteCodigo = FaseDaBase }.Reidratar();

    private static FaseCronograma FaseComJanela(int ordem) => new ModeloDaFase(ordem, CodigoDaFaseComJanela)
    {
        Inicio = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
        Fim = new DateTimeOffset(2026, 3, 10, 0, 0, 0, TimeSpan.Zero),
    }.Reidratar();

    // O estado só existe em processo carregado do banco, que não passa pela fábrica: a fábrica
    // recusa a âncora sem preliminar, então o preliminar sai da fase depois de ela pronta.
    private static FaseCronograma FaseComRecursoSemPreliminar(int ordem)
    {
        Guid preliminar = Guid.CreateVersion7();
        FaseCronograma fase = new ModeloDaFase(ordem, "FASE_COM_RECURSO")
        {
            Produtos =
            [
                ProdutoDaFase.Reidratar(preliminar, "RESULTADO_CLASSIFICACAO", PapelProdutoFase.Preliminar),
                ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo),
            ],
            RegraRecurso = RegraDeRecurso(preliminar),
        }.Reidratar();
        Lista<ProdutoDaFase>(fase, "_produtos").RemoveAll(static p => p.Papel == PapelProdutoFase.Preliminar);

        return fase;
    }

    private static FaseCronograma FaseComBancasIndistinguiveis(int ordem)
    {
        FaseCronograma fase = new ModeloDaFase(ordem, "FASE_COM_BANCAS").Reidratar();
        Lista<BancaRequerida>(fase, "_bancasRequeridas").AddRange([BancasDeTeste.DeRecursosDaFase(), BancasDeTeste.DeRecursosDaFase()]);

        return fase;
    }

    private static RegraRecursoFase RegraDeRecurso(Guid produtoAncoraId) => RegraRecursoFase.Criar(
        ReferenciaRegra.Criar(RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", new string('d', 64)).Value!,
        PrazoDoRecurso,
        produtoAncoraId).Value!;

    private static readonly ArgsRegraPrazoRecurso PrazoDoRecurso =
        new(2m, UnidadePrazo.DiasUteis, null, null, false, null, null);

    private static EtapaProcesso EtapaNaFase(string? faseCodigo, int ordem) => EtapaProcesso.Reidratar(
        Guid.CreateVersion7(), "Prova", CaraterEtapa.Classificatoria,
        TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!,
        peso: 1m, notaMinima: null, ordem, faseCodigo);

    private static EtapaProcesso EtapaComRecursoForaDosPreliminares()
    {
        EtapaProcesso etapa = EtapaNaFase(FaseDaBase, ordem: 1);
        RecursoDaEtapa janela = RecursoDaEtapa.Criar(
            AncoraDoRecurso.AtoPublicado,
            ReferenciaRegra.Criar(RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", new string('c', 64)).Value!,
            PrazoDoRecurso,
            Guid.CreateVersion7()).Value!;
        Lista<RecursoDaEtapa>(etapa, "_recursos").Add(janela);

        return etapa;
    }

    private static EtapaProcesso EtapaQueComecaAntesDaFase()
    {
        EtapaProcesso etapa = EtapaNaFase(CodigoDaFaseComJanela, ordem: 1);
        etapa.DefinirJanelaEParecer(
            new DateTimeOffset(2026, 2, 28, 8, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 3, 9, 18, 0, 0, TimeSpan.Zero),
            emiteParecerIndividual: false).IsSuccess.Should().BeTrue();

        return etapa;
    }

    private static void ComIsencaoDatada(CronogramaEmMontagem cronograma, DateTimeOffset aberturaDaIsencao, DateTimeOffset fimDaIsencao)
    {
        cronograma.Fases.Add(FaseDeInscricao(cronograma.ProximaOrdem, AberturaDasInscricoes, FimDasInscricoes));
        cronograma.Fases.Add(FaseDeIsencao(cronograma.ProximaOrdem, aberturaDaIsencao, fimDaIsencao));
    }

    private static List<T> Lista<T>(object dono, string campo) =>
        (List<T>)dono.GetType().GetField(campo, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(dono)!;
}
