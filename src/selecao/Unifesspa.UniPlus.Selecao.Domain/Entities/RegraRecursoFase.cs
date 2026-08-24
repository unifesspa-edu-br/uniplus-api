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
/// entidade consegue provar sozinha ficam aqui: a coerência da regra referenciada, as
/// unidades declaráveis e a magnitude do prazo de interposição, e a completude dos pares
/// de suspensividade.
/// </remarks>
/// <remarks>
/// Essas invariantes vivem aqui, e não só no validator da porta HTTP, porque existe
/// caminho de construção que não passa por ela: a reidratação do envelope chama
/// <see cref="Criar"/> direto ao restaurar configuração congelada. O validator continua
/// existindo e recusa antes, campo a campo, com mensagem por campo — quem chega pelo HTTP
/// recebe o 400 detalhado dele; quem chega por outro caminho encontra estas recusas, cada
/// uma com erro nomeado próprio.
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
    /// provar sozinho: a referência de catálogo por símbolo, as unidades declaráveis e a
    /// magnitude estritamente positiva do prazo de interposição (UNI-REQ-0081/0113), e a
    /// completude de cada par de suspensividade (UNI-REQ-0080).
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

        // UNI-REQ-0081: o valor congelado é sempre estritamente positivo. Zero fecharia a
        // janela no mesmo instante em que a abre, e negativo a fecharia antes do ato que a
        // ancora — nos dois casos o prazo nasce inutilizável, e a publicação o congela
        // assim.
        //
        // Vem antes de QUALQUER checagem de unidade de propósito. Um valor negativo em dia
        // corrido, ou fracionário negativo em dia útil, viola duas regras ao mesmo tempo, e
        // só a magnitude tem remediação que resolve: mandar quem declarou -5 dias corridos
        // reescrever em dias úteis o deixa com um prazo que continua fechando antes de
        // abrir. A recusa que orienta é a que sai primeiro.
        if (args.PrazoValor <= 0)
        {
            return Result<RegraRecursoFase>.Failure(new DomainError(
                "RegraRecursoFase.PrazoNaoPositivo",
                $"O prazo de interposição deve ser estritamente positivo — recebido '{args.PrazoValor}'."));
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

        // O que sobra do enum não é prazo declarável. Nenhuma é o valor zero e, portanto,
        // o default: um ArgsRegraPrazoRecurso construído sem preencher a unidade chega
        // aqui nesse estado, e aceitá-lo criaria uma regra de recurso cujo prazo não conta
        // em unidade nenhuma. A recusa é por lista do que vale, não do que não vale — uma
        // unidade nova no enum nasce recusada até ser declarada aqui de propósito.
        if (args.PrazoUnidade is not (UnidadePrazo.DiasUteis or UnidadePrazo.Horas))
        {
            return Result<RegraRecursoFase>.Failure(new DomainError(
                "RegraRecursoFase.PrazoSemUnidadeDeclaravel",
                $"O prazo de interposição exige unidade declarável — dias úteis ou horas; recebido '{args.PrazoUnidade}'."));
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

        Result primeiraInstancia = ValidarParDeSuspensividade(
            args.SuspensividadePrimeiraInstanciaValor,
            args.SuspensividadePrimeiraInstanciaUnidade,
            "1ª instância");
        if (primeiraInstancia.IsFailure)
        {
            return Result<RegraRecursoFase>.Failure(primeiraInstancia.Error!);
        }

        Result segundaInstancia = ValidarParDeSuspensividade(
            args.SuspensividadeSegundaInstanciaValor,
            args.SuspensividadeSegundaInstanciaUnidade,
            "2ª instância");
        if (segundaInstancia.IsFailure)
        {
            return Result<RegraRecursoFase>.Failure(segundaInstancia.Error!);
        }

        // A suspensividade admite as três unidades — dias corridos, que a interposição
        // acabou de recusar, e também dias úteis (UNI-REQ-0080 congela o par valor-unidade
        // com as três; a recusa de UNI-REQ-0113 alcança só a interposição): é outro
        // relógio, com outra regra. O que a contagem em dia útil exige — a convenção de
        // contagem declarada (UNI-REQ-0116) — é invariante do PROCESSO, não desta entidade,
        // porque a declaração é uma por certame e esta regra não enxerga a raiz.
        return Result<RegraRecursoFase>.Success(new RegraRecursoFase { Regra = regra, Args = args });
    }

    /// <summary>
    /// Valida um dos dois pares de suspensividade. UNI-REQ-0080 congela a suspensividade
    /// como par valor-unidade, nunca um valor numérico sozinho: um lado sem o outro não
    /// descreve janela alguma. A ausência dos dois é legítima e significa que aquela
    /// instância não bloqueia — é assim que o Ingresso desativa a 2ª instância, sem
    /// <c>if</c> por módulo.
    /// </summary>
    private static Result ValidarParDeSuspensividade(decimal? valor, UnidadePrazo? unidade, string instancia)
    {
        // A ausência dos dois é a desativação prevista da instância, e sai antes de qualquer
        // outra coisa: é assim que o Ingresso desliga a 2ª instância, sem `if` por módulo.
        if (valor is null && unidade is null)
        {
            return Result.Success();
        }

        // Cada metade presente é conferida ANTES de a incompletude ser reportada. Um par como
        // (valor: -3, unidade: ausente) erra duas coisas, e mandar completá-lo devolveria a
        // pessoa com uma janela negativa e uma segunda recusa — a orientação que resolve é a
        // que trata o valor que já está errado.
        if (valor is { } magnitude && magnitude <= 0)
        {
            return Result.Failure(new DomainError(
                "RegraRecursoFase.SuspensividadeNaoPositiva",
                $"O valor da suspensividade da {instancia} deve ser estritamente positivo — recebido '{magnitude}'."));
        }

        // Unidade presente e igual a Nenhuma é ausência disfarçada: passa por qualquer
        // checagem de presença e continua não dizendo em que unidade a janela corre. As três
        // unidades reais são declaráveis aqui — a recusa de dia corrido do UNI-REQ-0113
        // alcança só a interposição, porque a suspensividade é outro relógio.
        if (unidade is { } declarada
            && declarada is not (UnidadePrazo.Horas or UnidadePrazo.Dias or UnidadePrazo.DiasUteis))
        {
            return Result.Failure(new DomainError(
                "RegraRecursoFase.SuspensividadeUnidadeNaoDeclaravel",
                $"A unidade da suspensividade da {instancia} não é declarável — recebido '{declarada}'."));
        }

        // Sobrou o par com uma metade só, e ela é válida: aqui completar o par é, de fato, a
        // correção que resolve. UNI-REQ-0080 congela a suspensividade como par valor-unidade,
        // nunca um valor numérico sozinho — um lado sem o outro não descreve janela alguma.
        if (valor is null || unidade is null)
        {
            return Result.Failure(new DomainError(
                "RegraRecursoFase.SuspensividadeIncompleta",
                $"A suspensividade da {instancia} exige valor e unidade juntos, ou nenhum dos dois."));
        }

        return Result.Success();
    }

    internal void VincularFase(Guid faseCronogramaId) => FaseCronogramaId = faseCronogramaId;
}
