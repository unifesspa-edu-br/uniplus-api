namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fatos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// As fases declaradas de um processo, na ordem em que chegaram. Reúne as regras que dependem de mais de uma
/// fase, cada uma devolvendo todas as suas causas, e projeta nos itens do checklist as que o painel mostra.
/// </summary>
public sealed record CronogramaDoProcesso
{
    public CronogramaDoProcesso(IReadOnlyList<FaseDeclarada> fases)
    {
        ArgumentNullException.ThrowIfNull(fases);

        Fases = fases;
    }

    public IReadOnlyList<FaseDeclarada> Fases { get; }

    /// <summary>O cronograma das fases vivas, cada uma lida como declarou: as recusas do estado carregado do banco ficam à vista.</summary>
    public static CronogramaDoProcesso Do(IEnumerable<FaseCronograma> fases)
    {
        ArgumentNullException.ThrowIfNull(fases);

        return new([.. fases.Select(static fase => fase.Declaracao())]);
    }

    /// <summary>A fase de menor ordem entre as que coletam inscrição: a que ancora o período que o edital publica.</summary>
    public FaseDeclarada? FaseQueAncoraOPeriodoDeInscricao =>
        ReferenciaTemporalDosFatos.FaseQueAncoraOPeriodoDeInscricao(Fases, static fase => fase.ColetaInscricao, static fase => fase.Ordem);

    /// <summary>Se cada fase tem a sua origem: o casamento com o cronograma já gravado, que casa por origem, só existe sem repetição.</summary>
    public bool OrigensUnicas => Fases.Select(static fase => fase.OrigemId).Distinct().Count() == Fases.Count;

    /// <summary>Se alguma fase tem o código, ainda que mais de uma o tenha.</summary>
    public bool TemFaseDeCodigo(string codigo) => Fases.Any(fase => TemOCodigo(fase, codigo));

    /// <summary>Se mais de uma fase tem o código: nenhuma regra sabe qual delas o código designa.</summary>
    public bool CodigoAmbiguo(string codigo) => Fases.Count(fase => TemOCodigo(fase, codigo)) > 1;

    /// <summary>A fase do código, quando ele a designa sem ambiguidade; ausente também quando o código é ambíguo.</summary>
    public FaseDeclarada? FaseDeCodigo(string codigo)
    {
        FaseDeclarada[] candidatas = [.. Fases.Where(fase => TemOCodigo(fase, codigo))];

        return candidatas.Length == 1 ? candidatas[0] : null;
    }

    public IReadOnlyList<FieldError> RecusasDasDeclaracoes() =>
    [
        .. Fases.SelectMany(static (fase, posicao) => fase.Recusas.Select(recusa => NaFase(posicao, recusa.Field, recusa.Error))),
    ];

    /// <summary>As recusas das declarações cujo código de erro está entre os pedidos.</summary>
    public IReadOnlyList<FieldError> RecusasDasDeclaracoes(IReadOnlySet<string> codigos)
    {
        ArgumentNullException.ThrowIfNull(codigos);

        return [.. RecusasDasDeclaracoes().Where(recusa => codigos.Contains(recusa.Error.Code))];
    }

    /// <summary>
    /// O item de que as declarações das fases não têm recusa fora das que já têm item próprio (âncora do recurso, recorte de
    /// competência e recorte na banca de recursos): a conferência da configuração recusa toda declaração, e o painel mostra o que sobra aqui.
    /// </summary>
    public ItemConformidade ItemDasDeclaracoes() => new(
        "cronograma_fases_declaradas",
        DimensaoConformidade.Cronograma,
        "Cronograma: cada fase declara janela, publicações e bancas coerentes",
        RecusasDasDeclaracoes().Count == RecusasDasDeclaracoes(CodigosComItemProprio).Count);

    public IReadOnlyList<FieldError> ConferirEstrutura() => EstruturaDoCronograma.Recusas(Fases);

    /// <summary>
    /// O item de que as fases, em conjunto, não repetem ordem nem fase canônica: a mesma recusa da estrutura que a conferência
    /// da configuração chama, fora a ausência de fases, que já tem item próprio.
    /// </summary>
    public ItemConformidade ItemDasFasesEmConjunto() => new(
        "cronograma_fases_em_conjunto",
        DimensaoConformidade.Cronograma,
        "Cronograma: as fases não repetem ordem nem fase canônica",
        ConferirEstrutura().All(static recusa => recusa.Error.Code == EstruturaDoCronograma.CronogramaVazio));

    /// <summary>
    /// O que o processo exige do cronograma: a origem que recebe inscrição tem fase que a coleta, e as vagas ofertadas têm fase
    /// que produz resultado. A origem e a oferta de vagas chegam como valor: o cronograma só conhece as fases.
    /// </summary>
    public IReadOnlyList<FieldError> ConferirCronogramaExigidoPeloProcesso(OrigemCandidatos origem, bool haVagasOfertadas)
    {
        List<FieldError> causas = [];
        if (InscricaoPropriaSemFaseDeColeta(origem))
        {
            causas.Add(new FieldError("fases", SemFaseDeColeta()));
        }

        if (VagasSemFaseQueProduzResultado(haVagasOfertadas))
        {
            causas.Add(new FieldError("fases", SemFaseQueProduzResultado()));
        }

        return causas;
    }

    private static DomainError SemFaseDeColeta() => new(
        "ProcessoSeletivo.InscricaoPropriaSemFaseDeColeta",
        "A origem dos candidatos é inscrição própria, e nenhuma fase do cronograma coleta inscrição.");

    private static DomainError SemFaseQueProduzResultado() => new(
        "ProcessoSeletivo.VagasSemFaseQueProduzResultado",
        "Há vagas ofertadas, e nenhuma fase do cronograma produz resultado.");

    public ItemConformidade ItemDaInscricaoPropria(OrigemCandidatos origem) => new(
        "cronograma_inscricao_propria_sem_fase_de_coleta",
        DimensaoConformidade.Cronograma,
        "Cronograma: inscrição própria tem fase que coleta inscrição",
        !InscricaoPropriaSemFaseDeColeta(origem));

    public ItemConformidade ItemDasVagasOfertadas(bool haVagasOfertadas) => new(
        "cronograma_vagas_sem_fase_que_produz_resultado",
        DimensaoConformidade.Cronograma,
        "Cronograma: vagas ofertadas têm fase que produz resultado",
        !VagasSemFaseQueProduzResultado(haVagasOfertadas));

    /// <summary>O item de que o cronograma tem ao menos uma fase, derivado da mesma recusa da estrutura.</summary>
    public ItemConformidade ItemDaExistenciaDeFases() => new(
        "cronograma_fases_ausente",
        DimensaoConformidade.Cronograma,
        "Cronograma de fases",
        ConferirEstrutura().All(static recusa => recusa.Error.Code != EstruturaDoCronograma.CronogramaVazio));

    public IReadOnlyList<FieldError> ConferirPrecedencia(IReadOnlyList<ArestaPrecedencia> precedencias)
    {
        ArgumentNullException.ThrowIfNull(precedencias);

        return EstruturaDoCronograma.Precedencia(this, precedencias);
    }

    public IReadOnlyList<FieldError> ConferirConclusaoDoCiclo() => ConclusaoDoCicloRecursal.Recusas(this);

    /// <summary>As fases cuja concluinte declarada a conferência não pôde julgar, e que por isso não contam como conformes.</summary>
    public IReadOnlyList<FaseDeclarada> ConclusoesIndeterminadas() => ConclusaoDoCicloRecursal.Indeterminadas(this);

    /// <exception cref="InvalidOperationException">Há fase que coleta isenção com janela a conferir e o fuso institucional não foi informado.</exception>
    public IReadOnlyList<FieldError> ConferirCompletude(TimeZoneInfo? fuso) => CompletudeDoCronograma.Recusas(this, fuso);

    public ItemConformidade ItemDaAncoraDoRecurso() => ItemSemRecusa(
        "cronograma_ancora_do_recurso",
        "Cronograma: a regra de recurso ancora num produto preliminar da própria fase",
        AncoraDoRecursoDaFase.Codigos);

    public ItemConformidade ItemDoRecorteDeCompetencia() => ItemSemRecusa(
        "cronograma_recorte_de_competencia_das_bancas",
        "Cronograma: bancas do mesmo tipo na mesma fase declaram recortes de competência distintos",
        RecorteDeCompetenciaDasBancas.Codigos);

    public IReadOnlyList<ItemConformidade> ItensDaCompletude(TimeZoneInfo? fuso) => CompletudeDoCronograma.Itens(this, fuso);

    internal int PosicaoDe(FaseDeclarada fase)
    {
        for (int posicao = 0; posicao < Fases.Count; posicao++)
        {
            if (ReferenceEquals(Fases[posicao], fase))
            {
                return posicao;
            }
        }

        throw new ArgumentException("A fase não pertence a este cronograma.", nameof(fase));
    }

    /// <summary>Localiza a recusa na fase da posição; sem campo, o lugar é a própria fase.</summary>
    internal static FieldError NaFase(int posicao, string? campo, DomainError erro) =>
        new(string.IsNullOrEmpty(campo) ? $"fases[{posicao}]" : $"fases[{posicao}].{campo}", erro);

    // O piso mínimo vem da origem dos candidatos, nunca do tipo do processo.
    private bool InscricaoPropriaSemFaseDeColeta(OrigemCandidatos origem) =>
        origem == OrigemCandidatos.InscricaoPropria && !Fases.Any(static fase => fase.ColetaInscricao);

    private bool VagasSemFaseQueProduzResultado(bool haVagasOfertadas) =>
        haVagasOfertadas && !Fases.Any(static fase => fase.Atos.ProduzResultado);

    private static bool TemOCodigo(FaseDeclarada fase, string codigo) => string.Equals(fase.Codigo, codigo, StringComparison.Ordinal);

    private static readonly IReadOnlySet<string> CodigosComItemProprio = new HashSet<string>(
        [.. AncoraDoRecursoDaFase.Codigos, .. RecorteDeCompetenciaDasBancas.Codigos, .. ExigenciaDeBancaDeRecurso.CodigosDoRecorte],
        StringComparer.Ordinal);

    private ItemConformidade ItemSemRecusa(string codigo, string mensagem, IReadOnlySet<string> codigosDeRecusa) =>
        new(codigo, DimensaoConformidade.Cronograma, mensagem, RecusasDasDeclaracoes(codigosDeRecusa).Count == 0);
}
