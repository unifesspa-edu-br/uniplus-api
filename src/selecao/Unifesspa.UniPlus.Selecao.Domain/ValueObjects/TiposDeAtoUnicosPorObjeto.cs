namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Os tipos de ato que o catálogo de Publicações declara <b>únicos por objeto</b> — publicáveis
/// uma só vez por processo seletivo —, entre os que o cronograma declara, com o nome de cada um.
/// </summary>
/// <remarks>
/// Chega pronto da Application, que o lê do catálogo vigente: o agregado cita o tipo por código e
/// não conhece os atributos dele (ADR-0042). A unicidade é dado editável do catálogo, e por isso
/// nenhum código do agregado compara o tipo com um literal.
/// </remarks>
public sealed class TiposDeAtoUnicosPorObjeto
{
    private readonly Dictionary<string, string> _nomePorCodigo;

    public TiposDeAtoUnicosPorObjeto(IReadOnlyDictionary<string, string> nomePorCodigo)
    {
        ArgumentNullException.ThrowIfNull(nomePorCodigo);
        _nomePorCodigo = new Dictionary<string, string>(nomePorCodigo, StringComparer.Ordinal);
    }

    /// <summary>Nenhum tipo único — o estado de quem não consultou o catálogo.</summary>
    public static TiposDeAtoUnicosPorObjeto Nenhum { get; } = new(new Dictionary<string, string>());

    public bool EhUnico(string codigo) => _nomePorCodigo.ContainsKey(codigo);

    /// <summary>O nome do tipo único no catálogo, ou o próprio código quando o tipo não é único.</summary>
    public string NomeDe(string codigo) => _nomePorCodigo.GetValueOrDefault(codigo, codigo);
}
