namespace Unifesspa.UniPlus.Regras.Formularios;

using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Quando um grupo repetível aparece e quando é obrigatório (UNI-REQ-0146). Diferente das regras do
/// campo, o grupo não tem restrição de valor nem impedimento: só as duas regras que o decidem antes
/// das ocorrências.
/// </summary>
public sealed record ExibicaoEObrigatoriedade
{
    public ExibicaoEObrigatoriedade(PredicadoDnf? exibicao, Obrigatoriedade obrigatoriedade)
    {
        ArgumentNullException.ThrowIfNull(obrigatoriedade);

        Exibicao = exibicao;
        Obrigatoriedade = obrigatoriedade;
    }

    /// <summary>
    /// As regras para conferir só a forma do grupo quando a leitura delas foi recusada: a obrigatoriedade recusada ou
    /// ausente vale <c>Nunca</c> e a exibição recusada fica ausente, para não acusar o que a recusa própria já cobre.
    /// </summary>
    public static ExibicaoEObrigatoriedade ParaConferirAForma(PredicadoDnf? exibicao, Obrigatoriedade? obrigatoriedade) =>
        new(exibicao, obrigatoriedade ?? Obrigatoriedade.Nunca);

    /// <summary>Quando o grupo aparece; nulo quando aparece sempre.</summary>
    public PredicadoDnf? Exibicao { get; }

    public Obrigatoriedade Obrigatoriedade { get; }

    /// <summary>Os fatos que a exibição e a obrigatoriedade citam, sem repetição.</summary>
    public IReadOnlyCollection<string> FatosCitados =>
        [.. (Exibicao?.FatosCitados ?? []).Concat(Obrigatoriedade.FatosCitados).Distinct(StringComparer.Ordinal)];

    /// <summary>As condições da exibição e da obrigatoriedade, para os vínculos e as referências a valor do processo.</summary>
    public IEnumerable<CondicaoDnf> Condicoes =>
        (Exibicao?.Clausulas ?? []).Concat(Obrigatoriedade.Predicado?.Clausulas ?? []).SelectMany(static clausula => clausula.Condicoes);
}
