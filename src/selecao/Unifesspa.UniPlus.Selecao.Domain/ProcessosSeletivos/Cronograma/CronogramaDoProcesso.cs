namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
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

    public IReadOnlyList<FieldError> ConferirEstrutura() => EstruturaDoCronograma.Recusas(Fases);

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

    private static bool TemOCodigo(FaseDeclarada fase, string codigo) => string.Equals(fase.Codigo, codigo, StringComparison.Ordinal);

    private ItemConformidade ItemSemRecusa(string codigo, string mensagem, IReadOnlySet<string> codigosDeRecusa) =>
        new(codigo, DimensaoConformidade.Cronograma, mensagem, RecusasDasDeclaracoes(codigosDeRecusa).Count == 0);
}
