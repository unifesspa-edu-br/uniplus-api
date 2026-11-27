namespace Unifesspa.UniPlus.Regras.Formularios;

using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// As regras do campo sobre as respostas anteriores (UNI-REQ-0145): quando aparece, quando é
/// obrigatório, que valores aceita e que resposta impede a inscrição. O impedimento cita também a
/// resposta do próprio campo; as demais regras, só respostas anteriores.
/// </summary>
public sealed record RegrasDoCampo
{
    public RegrasDoCampo(
        PredicadoDnf? exibicao,
        Obrigatoriedade obrigatoriedade,
        IReadOnlyList<RestricaoValor> restricoes,
        Impedimento? impedimento = null)
    {
        ArgumentNullException.ThrowIfNull(obrigatoriedade);
        ArgumentNullException.ThrowIfNull(restricoes);

        Exibicao = exibicao;
        Obrigatoriedade = obrigatoriedade;
        Restricoes = [.. restricoes];
        Impedimento = impedimento;
    }

    /// <summary>Quando o campo aparece; nulo quando aparece sempre.</summary>
    public PredicadoDnf? Exibicao { get; }

    public Obrigatoriedade Obrigatoriedade { get; }

    /// <summary>As restrições sobre o valor respondido, na ordem em que foram dadas.</summary>
    public IReadOnlyList<RestricaoValor> Restricoes { get; }

    public Impedimento? Impedimento { get; }

    /// <summary>
    /// Os fatos de que o campo depende: os citados pela exibição, pela obrigatoriedade, pelas restrições
    /// e pelo impedimento, sem repetição. O impedimento cita a resposta do próprio campo, que não é
    /// dependência: só as respostas anteriores entram.
    /// </summary>
    public IReadOnlyCollection<string> FatosCitadosPor(string fatoDoCampo) =>
        [.. (Exibicao?.FatosCitados ?? []).Concat(Obrigatoriedade.FatosCitados).Concat(Restricoes.SelectMany(static r => r.FatosCitados))
            .Concat((Impedimento?.FatosCitados ?? []).Where(f => !string.Equals(f, fatoDoCampo, StringComparison.Ordinal)))
            .Distinct(StringComparer.Ordinal)];

    /// <summary>As mesmas regras, com as restrições na ordem do tipo, a ordem em que o campo as guarda.</summary>
    public RegrasDoCampo ComRestricoesNaOrdemDoTipo() => new(Exibicao, Obrigatoriedade, [.. Restricoes.OrderBy(static r => r.Tipo)], Impedimento);
}
