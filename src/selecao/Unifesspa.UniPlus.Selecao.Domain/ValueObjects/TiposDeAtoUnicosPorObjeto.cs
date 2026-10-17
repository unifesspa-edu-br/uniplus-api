namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Os tipos de ato <b>únicos por objeto</b> — publicáveis uma só vez por processo seletivo — que o
/// processo cita, segundo o catálogo vigente, os tipos dos atos já registrados do processo e o
/// tipo do ato da operação em curso, quando há.
/// </summary>
/// <remarks>
/// Chega pronto da Application, que o lê do catálogo vigente e dos atos já registrados: o
/// agregado cita o tipo por código e não conhece os atributos dele (ADR-0042). A unicidade é
/// dado editável do catálogo, e só ele a decide — um ato registrado apenas ocupa o tipo —,
/// como no registro do ato; por isso nenhum código do agregado compara o tipo com um literal. Os gates de publicação exigem este valor; não há instância padrão, para que
/// ninguém publique sem ter consultado o catálogo.
/// </remarks>
public sealed class TiposDeAtoUnicosPorObjeto
{
    private readonly Dictionary<string, string> _nomePorCodigo;
    private readonly HashSet<string> _ocupados;

    private TiposDeAtoUnicosPorObjeto(
        IReadOnlyDictionary<string, string> nomePorCodigo,
        IReadOnlyCollection<string> ocupados,
        string? atoDaOperacaoCodigo)
    {
        ArgumentNullException.ThrowIfNull(nomePorCodigo);
        ArgumentNullException.ThrowIfNull(ocupados);
        _nomePorCodigo = new Dictionary<string, string>(nomePorCodigo, StringComparer.Ordinal);
        _ocupados = new HashSet<string>(ocupados, StringComparer.Ordinal);
        AtoDaOperacaoCodigo = atoDaOperacaoCodigo;
    }

    /// <summary>
    /// Para publicar ou retificar. <paramref name="nomePorCodigo"/> são os tipos únicos que o
    /// catálogo vigente declara; <paramref name="ocupados"/>, os tipos dos atos já registrados do
    /// processo, únicos ou não; e o ato da operação também reivindica a vaga do seu tipo.
    /// </summary>
    public static TiposDeAtoUnicosPorObjeto ParaOperacao(
        IReadOnlyDictionary<string, string> nomePorCodigo,
        IReadOnlyCollection<string> ocupados,
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
        new(nomePorCodigo, [], null);

    /// <summary>O tipo do ato da publicação ou da retificação em curso, ou nulo no checklist.</summary>
    public string? AtoDaOperacaoCodigo { get; }

    /// <summary>Os códigos dos tipos dos atos já registrados do processo.</summary>
    public IReadOnlyCollection<string> CodigosOcupados => _ocupados;

    /// <summary>Se o catálogo vigente declara o tipo único por objeto.</summary>
    public bool EhUnico(string codigo) => _nomePorCodigo.ContainsKey(codigo);

    /// <summary>Se um ato já registrado do processo é desse tipo.</summary>
    public bool VagaOcupada(string codigo) => _ocupados.Contains(codigo);

    /// <summary>O nome do tipo único no catálogo, ou o próprio código quando o tipo não é único.</summary>
    public string NomeDe(string codigo) => _nomePorCodigo.GetValueOrDefault(codigo, codigo);
}
