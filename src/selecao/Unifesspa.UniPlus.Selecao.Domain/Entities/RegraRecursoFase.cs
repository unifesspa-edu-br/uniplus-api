namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Enums;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A regra de recurso de uma <see cref="FaseCronograma"/> (0..1, Story #851 §3.6): a
/// <b>presença</b> desta entidade é o que faz a fase admitir recurso — sem enum, sem
/// flag, sem lista de fases recorríveis em código.
/// </summary>
/// <remarks>
/// Deriva de <see cref="EntityBase"/> puro (sem soft-delete), mesmo padrão de
/// <see cref="EtapaProcesso"/>. As invariantes que dependem da fase-mãe (ProduzResultado,
/// ResultadoDefinitivo, AtoProduzidoCodigo — itens 1 e 2 do §3.6) são validadas por
/// <see cref="FaseCronograma.Criar"/>, que tem acesso aos dois lados; as que esta
/// entidade consegue provar sozinha (itens 6/7 — dias úteis proibido no prazo de
/// interposição, e indisponível na suspensividade enquanto o algoritmo de contagem não
/// for artefato versionado; a coerência da regra referenciada) ficam aqui.
/// </remarks>
public sealed class RegraRecursoFase : EntityBase
{
    public Guid FaseCronogramaId { get; private set; }

    public ReferenciaRegra Regra { get; private set; } = null!;

    public ArgsRegraPrazoRecurso Args { get; private set; } = null!;

    private RegraRecursoFase() { }

    /// <summary>
    /// Cria a regra de recurso da fase. Não resolve nem confere a existência da
    /// <paramref name="regra"/> no catálogo (isso é I/O — Application, via
    /// <c>IRegraCatalogoReader</c>, ADR-0042) nem a vigência do ato âncora (Application,
    /// via <c>ITipoAtoPublicadoReader</c>) — só as invariantes puras que este VO consegue
    /// provar sozinho.
    /// </summary>
    public static Result<RegraRecursoFase> Criar(ReferenciaRegra regra, ArgsRegraPrazoRecurso args)
    {
        ArgumentNullException.ThrowIfNull(regra);
        ArgumentNullException.ThrowIfNull(args);
        ArgumentException.ThrowIfNullOrWhiteSpace(args.AtoAncoraCodigo);

        // CA-01/CA-02: a regra referenciada só pode ser RECURSO-PRAZO-ANCORADO-EM-ATO —
        // qualquer outra (inclusive de outro TipoRegra) é recusada. A checagem completa
        // (regra existe, TipoRegra == RegraPrazoRecurso, hash bate) é do handler
        // (IRegraCatalogoReader, D9); esta é a defesa de forma, com o MESMO código de
        // erro, para que uma RegraRecursoFase nunca exista com uma referência incoerente
        // mesmo se construída fora do caminho do handler (ex.: reidratação do envelope).
        if (regra.Codigo != RegraPrazoRecursoCodigo.AncoradoEmAto)
        {
            return Result<RegraRecursoFase>.Failure(new DomainError(
                "RegraRecursoFase.RegraCatalogoInvalida",
                $"RegraRecursoFase só referencia a regra {RegraPrazoRecursoCodigo.AncoradoEmAto} — recebido '{regra.Codigo}'."));
        }

        // CA-20 (UNI-REQ-0080): a interposição em dias úteis é recusada de forma
        // PERMANENTE — não é limitação técnica temporária. É o prazo que fecha a porta do
        // candidato; um erro de contagem para menos cercearia direito, e nenhum calendário
        // ou algoritmo de contagem elimina esse risco. Nunca aproximado em silêncio; o
        // valor permanece representável no enum (vocabulário legal fechado).
        if (args.PrazoUnidade == UnidadePrazo.DiasUteis)
        {
            return Result<RegraRecursoFase>.Failure(new DomainError(
                "RegraRecursoFase.PrazoEmDiasUteisSemCalendario",
                "O prazo de interposição deve ser informado em horas ou dias corridos; dias úteis não são aceitos."));
        }

        // CA-21 (UNI-REQ-0080): a suspensividade em dias úteis é condicionalmente
        // aceitável — mas exige, ao mesmo tempo, um calendário de dias úteis vigente E
        // uma versão identificável do algoritmo de contagem, ambas congeladas no
        // snapshot. Nenhum artefato de algoritmo versionado existe hoje (o motor de
        // contagem é UNI-REQ-0081, incremento futuro), então a recusa vale sempre, por
        // ora — checagem POR INSTÂNCIA, independente: qualquer uma das duas em dias
        // úteis recusa, mesmo que a outra esteja em dias corridos ou seja null.
        if (args.SuspensividadePrimeiraInstanciaUnidade == UnidadePrazo.DiasUteis
            || args.SuspensividadeSegundaInstanciaUnidade == UnidadePrazo.DiasUteis)
        {
            return Result<RegraRecursoFase>.Failure(new DomainError(
                "RegraRecursoFase.SuspensividadeEmDiasUteisSemCalendario",
                "A suspensividade em dias úteis, em qualquer uma das duas instâncias, exige calendário vigente e algoritmo de contagem versionado; o algoritmo ainda não está disponível."));
        }

        return Result<RegraRecursoFase>.Success(new RegraRecursoFase { Regra = regra, Args = args });
    }

    internal void VincularFase(Guid faseCronogramaId) => FaseCronogramaId = faseCronogramaId;
}
