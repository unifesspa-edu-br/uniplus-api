namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Os tipos de ato que o catálogo de Publicações declara <b>únicos por objeto</b> — publicáveis
/// uma só vez por processo seletivo —, entre os que o processo cita, com o nome de cada um, e o
/// tipo do ato da operação em curso, quando há.
/// </summary>
/// <remarks>
/// Chega pronto da Application, que o lê do catálogo vigente: o agregado cita o tipo por código e
/// não conhece os atributos dele (ADR-0042). A unicidade é dado editável do catálogo, e por isso
/// nenhum código do agregado compara o tipo com um literal. Os gates de publicação exigem este
/// valor; não há instância padrão, para que ninguém publique sem ter consultado o catálogo.
/// </remarks>
public sealed class TiposDeAtoUnicosPorObjeto
{
    private readonly Dictionary<string, string> _nomePorCodigo;

    private TiposDeAtoUnicosPorObjeto(IReadOnlyDictionary<string, string> nomePorCodigo, string? atoDaOperacaoCodigo)
    {
        ArgumentNullException.ThrowIfNull(nomePorCodigo);
        _nomePorCodigo = new Dictionary<string, string>(nomePorCodigo, StringComparer.Ordinal);
        AtoDaOperacaoCodigo = atoDaOperacaoCodigo;
    }

    /// <summary>
    /// Para publicar ou retificar: o ato da operação também ocupa a vaga do tipo único, então o
    /// seu tipo conta como declaração.
    /// </summary>
    public static TiposDeAtoUnicosPorObjeto ParaOperacao(IReadOnlyDictionary<string, string> nomePorCodigo, string atoDaOperacaoCodigo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(atoDaOperacaoCodigo);
        return new(nomePorCodigo, atoDaOperacaoCodigo);
    }

    /// <summary>
    /// Para o checklist de conformidade, que avalia o processo antes de haver ato: só o
    /// cronograma conta.
    /// </summary>
    public static TiposDeAtoUnicosPorObjeto ParaChecklist(IReadOnlyDictionary<string, string> nomePorCodigo) =>
        new(nomePorCodigo, null);

    /// <summary>O tipo do ato da publicação ou da retificação em curso, ou nulo no checklist.</summary>
    public string? AtoDaOperacaoCodigo { get; }

    public bool EhUnico(string codigo) => _nomePorCodigo.ContainsKey(codigo);

    /// <summary>O nome do tipo único no catálogo, ou o próprio código quando o tipo não é único.</summary>
    public string NomeDe(string codigo) => _nomePorCodigo.GetValueOrDefault(codigo, codigo);
}
