namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O que o catálogo e os atos já registrados dizem sobre os tipos de ato <b>únicos por objeto</b>
/// — publicáveis uma só vez por processo seletivo — que o processo cita, e o tipo do ato da
/// operação em curso, quando há.
/// </summary>
/// <remarks>
/// Chega pronto da Application, que o lê do catálogo vigente e dos atos já registrados: o
/// agregado cita o tipo por código e não conhece os atributos dele (ADR-0042). A unicidade é
/// dado editável do catálogo, e por isso nenhum código do agregado compara o tipo com um
/// literal. Os gates de publicação exigem este valor; não há instância padrão, para que
/// ninguém publique sem ter consultado o catálogo.
/// </remarks>
public sealed class TiposDeAtoUnicosPorObjeto
{
    private readonly Dictionary<string, string> _nomePorCodigo;
    private readonly Dictionary<string, string> _ocupados;

    private TiposDeAtoUnicosPorObjeto(
        IReadOnlyDictionary<string, string> nomePorCodigo,
        IReadOnlyDictionary<string, string> ocupados,
        string? atoDaOperacaoCodigo)
    {
        ArgumentNullException.ThrowIfNull(nomePorCodigo);
        ArgumentNullException.ThrowIfNull(ocupados);
        _nomePorCodigo = new Dictionary<string, string>(nomePorCodigo, StringComparer.Ordinal);
        _ocupados = new Dictionary<string, string>(ocupados, StringComparer.Ordinal);
        AtoDaOperacaoCodigo = atoDaOperacaoCodigo;
    }

    /// <summary>
    /// Para publicar ou retificar. <paramref name="nomePorCodigo"/> são os tipos únicos que o
    /// catálogo vigente declara; <paramref name="ocupados"/>, os que os atos já registrados do
    /// processo ocupam; e o ato da operação também reivindica a vaga do seu tipo.
    /// </summary>
    public static TiposDeAtoUnicosPorObjeto ParaOperacao(
        IReadOnlyDictionary<string, string> nomePorCodigo,
        IReadOnlyDictionary<string, string> ocupados,
        string atoDaOperacaoCodigo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(atoDaOperacaoCodigo);
        return new(nomePorCodigo, ocupados, atoDaOperacaoCodigo);
    }

    /// <summary>
    /// Para o checklist de conformidade, que avalia o processo antes de haver ato: só o
    /// cronograma conta.
    /// </summary>
    public static TiposDeAtoUnicosPorObjeto ParaChecklist(IReadOnlyDictionary<string, string> nomePorCodigo) =>
        new(nomePorCodigo, new Dictionary<string, string>(), null);

    /// <summary>O tipo do ato da publicação ou da retificação em curso, ou nulo no checklist.</summary>
    public string? AtoDaOperacaoCodigo { get; }

    /// <summary>Os códigos dos tipos únicos cuja vaga já está ocupada por ato registrado do processo.</summary>
    public IReadOnlyCollection<string> CodigosOcupados => _ocupados.Keys;

    public bool EhUnico(string codigo) => _nomePorCodigo.ContainsKey(codigo) || _ocupados.ContainsKey(codigo);

    public bool VagaOcupada(string codigo) => _ocupados.ContainsKey(codigo);

    /// <summary>O nome do tipo único, ou o próprio código quando o tipo não é único.</summary>
    public string NomeDe(string codigo) =>
        _nomePorCodigo.TryGetValue(codigo, out string? nome) ? nome : _ocupados.GetValueOrDefault(codigo, codigo);
}
