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
/// entidade consegue provar sozinha ficam aqui: a coerência da regra referenciada e as
/// unidades declaráveis no prazo de interposição.
/// </remarks>
/// <remarks>
/// A convenção de contagem que o certame usa <b>não</b> é verificada aqui. Ela é uma por
/// processo, e esta entidade não enxerga a raiz — o gate correspondente é invariante de
/// <see cref="ProcessoSeletivo"/>, aplicado nas transições que geram versão.
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

        // UNI-REQ-0113: o prazo de interposição corre exclusivamente em dia útil. É o
        // prazo que fecha a porta do candidato, e tempo que passa quando ele não tem como
        // agir não pode consumir a janela — dia corrido a encolheria sempre que calhasse
        // de cair em feriado. Só duas unidades são declaráveis: dias úteis em valor
        // inteiro, e horas, que só avançam o relógio quando situadas em dia útil.
        if (args.PrazoUnidade == UnidadePrazo.Dias)
        {
            return Result<RegraRecursoFase>.Failure(new DomainError(
                "RegraRecursoFase.PrazoEmDiasCorridos",
                "O prazo de interposição deve ser informado em dias úteis ou horas; dias corridos não são aceitos."));
        }

        // UNI-REQ-0113: fração de dia útil não tem leitura unívoca — meio expediente,
        // doze horas dentro do dia, ou metade de um dia civil que, numa transição de
        // fuso, nem sempre tem vinte e quatro horas. As três fecham a janela em instantes
        // diferentes, então a declaração é recusada em vez de eleger uma em silêncio.
        // Prazo menor que um dia se declara em horas.
        if (args.PrazoUnidade == UnidadePrazo.DiasUteis && decimal.Truncate(args.PrazoValor) != args.PrazoValor)
        {
            return Result<RegraRecursoFase>.Failure(new DomainError(
                "RegraRecursoFase.PrazoEmFracaoDeDiaUtil",
                $"O prazo de interposição em dias úteis exige valor inteiro — recebido '{args.PrazoValor}'. Para prazo menor que um dia, declare em horas."));
        }

        // A suspensividade admite as três unidades (UNI-REQ-0116), inclusive dias úteis:
        // é outro relógio, com outra regra. O que a contagem em dia útil exige — a
        // convenção de contagem declarada — é invariante do PROCESSO, não desta entidade,
        // porque a declaração é uma por certame e esta regra não enxerga a raiz.
        return Result<RegraRecursoFase>.Success(new RegraRecursoFase { Regra = regra, Args = args });
    }

    internal void VincularFase(Guid faseCronogramaId) => FaseCronogramaId = faseCronogramaId;
}
