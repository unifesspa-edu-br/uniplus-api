namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;

/// <summary>
/// A regra de homologação das inscrições que o edital declara na fase: o resultado que publica a lista
/// e as alternativas (OU) de condições (E) que homologam a inscrição. Sem alternativa, a fase homologa
/// toda inscrição enviada (UNI-REQ-0265).
/// </summary>
public sealed record RegraDeHomologacaoDasInscricoes
{
    private RegraDeHomologacaoDasInscricoes(string? resultadoAtoCodigo, IReadOnlyList<AlternativaDeCondicoes> alternativas)
    {
        ResultadoAtoCodigo = resultadoAtoCodigo;
        Alternativas = alternativas;
    }

    /// <summary>O código do tipo de ato do produto da fase que leva a lista de homologação; ausente quando a regra não o aponta.</summary>
    public string? ResultadoAtoCodigo { get; }

    public IReadOnlyList<AlternativaDeCondicoes> Alternativas { get; }

    public bool HomologaTodaInscricaoEnviada => Alternativas.Count == 0;

    /// <summary>Se a outra regra aponta o mesmo resultado e tem as mesmas alternativas, na mesma ordem: a ordem muda os motivos da inscrição.</summary>
    public bool MesmoConteudo(RegraDeHomologacaoDasInscricoes outra)
    {
        ArgumentNullException.ThrowIfNull(outra);
        return string.Equals(ResultadoAtoCodigo, outra.ResultadoAtoCodigo, StringComparison.Ordinal)
            && Alternativas.Count == outra.Alternativas.Count
            && Alternativas.Zip(outra.Alternativas).All(static par => par.First.MesmoConteudo(par.Second));
    }

    /// <summary>
    /// Declara a regra como veio, sem recusa própria: o resultado ausente fica ausente e é conferido contra os
    /// produtos da fase, que é onde se sabe se ele existe.
    /// </summary>
    public static RegraDeHomologacaoDasInscricoes Declarar(string? resultadoAtoCodigo, IReadOnlyList<AlternativaDeCondicoes> alternativas)
    {
        ArgumentNullException.ThrowIfNull(alternativas);
        if (alternativas.Any(static alternativa => alternativa is null))
        {
            throw new ArgumentException("A regra não admite alternativa nula.", nameof(alternativas));
        }

        return new RegraDeHomologacaoDasInscricoes(
            string.IsNullOrWhiteSpace(resultadoAtoCodigo) ? null : resultadoAtoCodigo.Trim(),
            [.. alternativas]);
    }
}
