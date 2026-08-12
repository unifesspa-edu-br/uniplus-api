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
/// <see cref="FaseCronograma.Criar"/>, que tem acesso aos dois lados; a que esta
/// entidade consegue provar sozinha (a coerência da regra referenciada) fica aqui.
/// DIAS_UTEIS sem calendário vigente ou sem localidade resolvível (issue #1113) NÃO é
/// invariante deste VO — exige I/O (<c>ICalendarioVigenteReader</c>) e é competência do
/// handler (<c>DefinirCronogramaFasesCommandHandler</c>).
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
    /// <c>IRegraCatalogoReader</c>, ADR-0042), a vigência do ato âncora (Application,
    /// via <c>ITipoAtoPublicadoReader</c>) nem a vigência do calendário de dias úteis ou
    /// a localidade da unidade administradora (Application, via
    /// <c>ICalendarioVigenteReader</c>, issue #1113) — só as invariantes puras que este
    /// VO consegue provar sozinho. <c>DiasUteis</c> é aceito aqui como qualquer outra
    /// <see cref="UnidadePrazo"/>: este método também roda na reidratação do envelope
    /// (<c>EnvelopeCodecV11.LerRegraRecursoFase</c>), onde não há I/O disponível para
    /// reconferir se o calendário que valeu na declaração ainda é o vigente hoje — e não
    /// deveria haver, já que reidratar histórico não pode falhar por o presente ter
    /// mudado.
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

        return Result<RegraRecursoFase>.Success(new RegraRecursoFase { Regra = regra, Args = args });
    }

    internal void VincularFase(Guid faseCronogramaId) => FaseCronogramaId = faseCronogramaId;
}
