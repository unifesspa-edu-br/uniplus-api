namespace Unifesspa.UniPlus.Regras.Formularios;

using System.Text.Json;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Um item do formulário: o fato que ele produz e as regras do item — exibição, obrigatoriedade,
/// restrições de valor, impedimento (UNI-REQ-0145) e, no campo de texto, o formato do fato no
/// catálogo.
/// </summary>
public sealed record DefinicaoItem
{
    public DefinicaoItem(
        string fatoCodigo,
        PredicadoDnf? exibicao,
        Obrigatoriedade obrigatoriedade,
        IReadOnlyList<RestricaoValor> restricoes,
        Impedimento? impedimento = null,
        string? formato = null)
    {
        if (Violacao(fatoCodigo, formato) is { } violacao)
        {
            throw new ArgumentException(violacao, nameof(fatoCodigo));
        }
        ArgumentNullException.ThrowIfNull(obrigatoriedade);
        ArgumentNullException.ThrowIfNull(restricoes);
        FatoCodigo = fatoCodigo;
        Exibicao = exibicao;
        Obrigatoriedade = obrigatoriedade;
        Restricoes = [.. restricoes];
        Impedimento = impedimento;
        formato = ConjuntoBasicoDaInscricao.FormatoDaResposta(fatoCodigo, formato);
        Formato = formato;
        RestricoesDaResposta = formato is null ? Restricoes : [.. Restricoes, new FormatoDeTexto(formato)];
    }

    /// <summary>A recusa do item sem código do fato, ou cujo formato, no campo de texto, não é do catálogo.</summary>
    internal static string? Violacao(string? fatoCodigo, string? formato) =>
        string.IsNullOrWhiteSpace(fatoCodigo) ? "Um campo do formulário não tem o código do fato."
        : ConjuntoBasicoDaInscricao.FormatoDaResposta(fatoCodigo, formato) is { } daResposta ? FormatoDeTexto.Violacao(daResposta)
        : null;

    public string FatoCodigo { get; }

    /// <summary>A condição de exibição do item — nula quando o item sempre aparece na página.</summary>
    public PredicadoDnf? Exibicao { get; }

    public Obrigatoriedade Obrigatoriedade { get; }

    public IReadOnlyList<RestricaoValor> Restricoes { get; }

    /// <summary>A resposta que impede a inscrição — nulo quando nenhuma impede.</summary>
    public Impedimento? Impedimento { get; }

    /// <summary>O formato do fato no catálogo, no campo de texto; nulo nos demais.</summary>
    public string? Formato { get; }

    /// <summary>O que a resposta tem de atender: as restrições declaradas e o formato do campo.</summary>
    public IReadOnlyList<RestricaoValor> RestricoesDaResposta { get; }

    internal (IReadOnlyList<RestricaoValor> Violadas, bool Indeterminada) ConferirResposta(JsonElement resposta, ContextoDaResposta contexto)
    {
        ConferenciaDaResposta conferencia = new();
        foreach (RestricaoValor restricao in RestricoesDaResposta)
        {
            conferencia.Conferir(restricao, resposta, contexto);
        }
        return (conferencia.Violadas, conferencia.Indeterminada);
    }

    internal OpcoesVigentes? OpcoesVigentes(IReadOnlyDictionary<string, FatoResolvido> fatos) =>
        RestricoesDaResposta.Select(r => r.Opcoes(fatos)).OfType<OpcoesVigentes>()
            .Aggregate((OpcoesVigentes?)null, static (juntas, uma) => juntas?.E(uma) ?? uma);

    internal Ternario Impedido(FatoResolvido fato, IReadOnlyDictionary<string, FatoResolvido> fatos) =>
        Impedimento?.Avaliar(new Dictionary<string, FatoResolvido>(fatos, StringComparer.Ordinal) { [FatoCodigo] = fato })
            ?? Ternario.Falso;

    /// <summary>
    /// Os fatos citados pelas regras do próprio item, sem a exibição da página. O impedimento cita a
    /// resposta do próprio campo, que não é dependência dele: só as respostas anteriores entram.
    /// </summary>
    public IReadOnlyCollection<string> FatosCitados =>
        [.. (Exibicao?.FatosCitados ?? [])
            .Concat(Obrigatoriedade.FatosCitados)
            .Concat(Restricoes.SelectMany(static r => r.FatosCitados))
            .Concat((Impedimento?.FatosCitados ?? []).Where(f => !string.Equals(f, FatoCodigo, StringComparison.Ordinal)))
            .Distinct(StringComparer.Ordinal)];
}

