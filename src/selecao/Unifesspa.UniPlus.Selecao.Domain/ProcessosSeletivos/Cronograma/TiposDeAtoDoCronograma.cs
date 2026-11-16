namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public static class TiposDeAtoDoCronograma
{
    /// <summary>
    /// A primeira pendência de um tipo de ato único por objeto declarado mais de uma vez no processo — nos produtos das fases
    /// e das etapas e, quando há, no ato da própria operação —, ou <see langword="null"/> quando cada um deles é declarado
    /// uma só vez.
    /// </summary>
    /// <remarks>
    /// O tipo único só admite um ato por processo (ADR-0107): declará-lo duas vezes faz o edital sair publicado, o primeiro
    /// ato ser registrado e o segundo ser recusado na fila semanas depois, com o edital já congelado. O ato da publicação ou
    /// da retificação ocupa a mesma vaga, então um produto do cronograma do mesmo tipo é declaração repetida. Declarar o mesmo
    /// tipo como preliminar e como definitivo conta como duas declarações: o tipo único é só o ato que sai uma vez no
    /// processo, sem preliminar e definitivo, e a matéria que sai nos dois papéis ou em mais de um ciclo usa tipo não único.
    /// Quais tipos são únicos é dado do catálogo, que chega em <paramref name="tiposUnicos"/> — o agregado não o conhece
    /// (ADR-0042).
    /// </remarks>
    public static DomainError? PendenciaDoTipoDeAtoUnico(EstadoDaConfiguracao estado, TiposDeAtoUnicosPorObjeto tiposUnicos) =>
        PrimeiraRecusa(estado, tiposUnicos, ConferenciaDosTiposDeAto.ConferirTiposUnicosRepetidos);

    /// <summary>
    /// O primeiro produto declarado sem papel cujo tipo é resultado no catálogo, ou <see langword="null"/>. O resultado sem
    /// preliminar é declarado definitivo: só o ato que não é resultado fica sem papel (UNI-REQ-0157). A classificação é dado
    /// do catálogo e chega em <paramref name="tipos"/>; tipo sem versão vigente não tem classificação e fica de fora, e quem o
    /// recusa é a conferência do tipo.
    /// </summary>
    public static DomainError? PendenciaDoResultadoSemPapel(EstadoDaConfiguracao estado, TiposDeAtoUnicosPorObjeto tipos) =>
        PrimeiraRecusa(estado, tipos, ConferenciaDosTiposDeAto.ConferirResultadosSemPapel);

    /// <summary>
    /// O primeiro produto declarado com papel cujo tipo congela a configuração, ou <see langword="null"/>. A correção de um
    /// resultado não cria versão nova (UNI-REQ-0156), então um tipo que congela não teria correção possível como item de
    /// resultado.
    /// </summary>
    public static DomainError? PendenciaDoTipoQueCongelaDeclaradoComoResultado(EstadoDaConfiguracao estado, TiposDeAtoUnicosPorObjeto tipos) =>
        PrimeiraRecusa(estado, tipos, ConferenciaDosTiposDeAto.ConferirTiposQueCongelamComoResultado);

    /// <summary>
    /// As vagas que a operação e os atos já registrados reivindicam, uma por tipo: o ato da operação e o ato já registrado do
    /// mesmo tipo são a mesma vaga — a retificação continua a própria linhagem, não disputa com ela —, e por isso contam uma
    /// vez só.
    /// </summary>
    internal static IEnumerable<DeclaracaoDeAto> VagasJaReivindicadas(TiposDeAtoUnicosPorObjeto tiposUnicos) =>
    [
        .. tiposUnicos.CodigosOcupados
            .Where(codigo => OcupaVagaUnica(tiposUnicos, codigo))
            .Select(static codigo => new DeclaracaoDeAto(codigo, "no ato já registrado do processo")),
        .. VagaDoAtoDaOperacao(tiposUnicos),
    ];

    /// <summary>
    /// Cada produto que o cronograma declara, o das fases e o das etapas, com o lugar que o declara. É a fonte única dos atos
    /// declarados: quem precisa saber quais tipos de ato o processo cita lê daqui. A etapa é identificada pela ordem além do
    /// nome, porque o nome não é único: duas etapas de mesmo nome em lugares distintos seriam contadas como uma.
    /// </summary>
    public static IReadOnlyList<DeclaracaoDeAto> DeclaracoesDeAtos(EstadoDaConfiguracao estado)
    {
        ArgumentNullException.ThrowIfNull(estado);

        return [
            .. estado.CronogramaFases.SelectMany(static fase => fase.Produtos
                .Select(produto => new DeclaracaoDeAto(produto.AtoCodigo, LugarDaDeclaracao.DaFase(fase.Codigo), produto.Papel))),
            .. estado.Etapas.SelectMany(static etapa => etapa.Produtos
                .Select(produto => new DeclaracaoDeAto(produto.AtoCodigo, LugarDaDeclaracao.DaEtapa(etapa.Nome, etapa.Ordem), produto.Papel))),
        ];
    }

    private static DomainError? PrimeiraRecusa(
        EstadoDaConfiguracao estado,
        TiposDeAtoUnicosPorObjeto tipos,
        Func<CronogramaDoProcesso, EtapasDoProcesso, TiposDeAtoUnicosPorObjeto, IReadOnlyList<FieldError>> conferir)
    {
        ArgumentNullException.ThrowIfNull(estado);

        return CausasDaRecusa.Primeira(conferir(CronogramaDoProcesso.Do(estado.CronogramaFases), EtapasDoProcesso.Das(estado.Etapas), tipos));
    }

    private static bool OcupaVagaUnica(TiposDeAtoUnicosPorObjeto tiposUnicos, string codigo) =>
        tiposUnicos.EhUnico(codigo)
        || (tiposUnicos.AtoDaOperacaoEhUnico && string.Equals(codigo, tiposUnicos.AtoDaOperacaoCodigo, StringComparison.Ordinal));

    private static IEnumerable<DeclaracaoDeAto> VagaDoAtoDaOperacao(TiposDeAtoUnicosPorObjeto tiposUnicos) =>
        tiposUnicos.AtoDaOperacaoCodigo is { } atoDaOperacao && tiposUnicos.AtoDaOperacaoEhUnico && !tiposUnicos.VagaOcupada(atoDaOperacao)
            ? [new DeclaracaoDeAto(atoDaOperacao, "no ato que está sendo registrado")]
            : [];
}
