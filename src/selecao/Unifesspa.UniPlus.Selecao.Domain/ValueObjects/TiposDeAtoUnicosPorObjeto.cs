namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O que o catálogo vigente diz dos tipos de ato que o processo cita, para os gates de publicação:
/// quais são <b>únicos por objeto</b> — publicáveis uma só vez por processo seletivo —, a
/// classificação de cada um como resultado ou como congelante, os tipos dos atos já registrados do
/// processo e o tipo do ato da operação em curso, quando há.
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
    private readonly Dictionary<string, ClassificacaoDoTipoDeAto> _classificacoes;
    private readonly string? _nomeDoAtoDaOperacao;

    private TiposDeAtoUnicosPorObjeto(
        IReadOnlyDictionary<string, string> nomePorCodigo,
        IReadOnlyCollection<string> ocupados,
        string? atoDaOperacaoCodigo,
        IReadOnlyDictionary<string, ClassificacaoDoTipoDeAto>? classificacoes,
        bool atoDaOperacaoEhUnico = false,
        string? nomeDoAtoDaOperacao = null)
    {
        ArgumentNullException.ThrowIfNull(nomePorCodigo);
        ArgumentNullException.ThrowIfNull(ocupados);
        _nomePorCodigo = new Dictionary<string, string>(nomePorCodigo, StringComparer.Ordinal);
        _classificacoes = new Dictionary<string, ClassificacaoDoTipoDeAto>(
            classificacoes ?? new Dictionary<string, ClassificacaoDoTipoDeAto>(), StringComparer.Ordinal);
        _ocupados = new HashSet<string>(ocupados, StringComparer.Ordinal);
        AtoDaOperacaoCodigo = atoDaOperacaoCodigo;
        AtoDaOperacaoEhUnico = atoDaOperacaoEhUnico;
        _nomeDoAtoDaOperacao = nomeDoAtoDaOperacao;
    }

    /// <summary>
    /// Para publicar ou retificar. <paramref name="nomePorCodigo"/> são os tipos únicos que o
    /// catálogo vigente declara; <paramref name="ocupados"/>, os tipos dos atos já registrados do
    /// processo, únicos ou não; e o ato da operação também reivindica a vaga do seu tipo.
    /// </summary>
    public static TiposDeAtoUnicosPorObjeto ParaOperacao(
        IReadOnlyDictionary<string, string> nomePorCodigo,
        IReadOnlyCollection<string> ocupados,
        string atoDaOperacaoCodigo,
        IReadOnlyDictionary<string, ClassificacaoDoTipoDeAto>? classificacoes = null,
        bool atoDaOperacaoEhUnico = false,
        string? nomeDoAtoDaOperacao = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(atoDaOperacaoCodigo);
        return new(nomePorCodigo, ocupados, atoDaOperacaoCodigo, classificacoes, atoDaOperacaoEhUnico, nomeDoAtoDaOperacao);
    }

    /// <summary>
    /// Para o checklist de conformidade, que avalia o processo antes de haver ato da operação: o
    /// cronograma e os atos já registrados do processo contam, e o tipo do ato que ainda será
    /// registrado não é conhecido.
    /// </summary>
    public static TiposDeAtoUnicosPorObjeto ParaChecklist(
        IReadOnlyDictionary<string, string> nomePorCodigo,
        IReadOnlyCollection<string> ocupados,
        IReadOnlyDictionary<string, ClassificacaoDoTipoDeAto>? classificacoes = null) =>
        new(nomePorCodigo, ocupados, null, classificacoes);

    /// <summary>O tipo do ato da publicação ou da retificação em curso, ou nulo no checklist.</summary>
    public string? AtoDaOperacaoCodigo { get; }

    /// <summary>
    /// Se o tipo do ato da operação é único. É o atributo do próprio ato, para a vaga dele, e não se funde
    /// com a classificação de um item do cronograma de mesmo código, que é a congelada na versão.
    /// </summary>
    public bool AtoDaOperacaoEhUnico { get; }

    /// <summary>Os códigos dos tipos dos atos já registrados do processo.</summary>
    public IReadOnlyCollection<string> CodigosOcupados => _ocupados;

    /// <summary>Se o catálogo vigente declara o tipo único por objeto.</summary>
    public bool EhUnico(string codigo) => _nomePorCodigo.ContainsKey(codigo);

    /// <summary>Se um ato já registrado do processo é desse tipo.</summary>
    public bool VagaOcupada(string codigo) => _ocupados.Contains(codigo);

    /// <summary>
    /// A classificação do tipo no catálogo vigente, ou nulo quando o catálogo não tem versão vigente
    /// dele — quem recusa o tipo desconhecido é a conferência do tipo declarado, não esta.
    /// </summary>
    public ClassificacaoDoTipoDeAto? ClassificacaoDe(string codigo) =>
        _classificacoes.GetValueOrDefault(codigo);

    /// <summary>O nome do tipo único no catálogo, ou o próprio código quando o tipo não é único.</summary>
    public string NomeDe(string codigo) =>
        _nomePorCodigo.GetValueOrDefault(
            codigo,
            string.Equals(codigo, AtoDaOperacaoCodigo, StringComparison.Ordinal) ? _nomeDoAtoDaOperacao ?? codigo : codigo);
}
