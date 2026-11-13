namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O que a manifestação técnica diz: o parecer e o texto, que andam juntos porque o texto é obrigatório nos pareceres
/// desfavoráveis (UNI-REQ-0373).
/// </summary>
public sealed record ConteudoDaManifestacao
{
    private ConteudoDaManifestacao(ParecerDaManifestacao parecer, Fundamentacao? texto)
    {
        Parecer = parecer;
        Texto = texto;
    }

    /// <summary>Adequada, recomenda ajuste ou inadequada.</summary>
    public ParecerDaManifestacao Parecer { get; }

    /// <summary>O texto da manifestação; opcional só no parecer adequada.</summary>
    public Fundamentacao? Texto { get; }

    /// <summary>Recomenda ajuste ou inadequada: vigente, exige a justificativa de quem aprova ou recomenda ajuste.</summary>
    public bool Desfavoravel => PareceresDaManifestacao.EhDesfavoravel(Parecer);

    /// <summary>
    /// O conteúdo informado, ou <see langword="null"/> quando há recusa. Acumula no lote (ADR-0125) o parecer ausente
    /// ou fora do vocabulário, o texto ausente no parecer desfavorável e o texto longo demais.
    /// </summary>
    public static ConteudoDaManifestacao? Ler(string? parecer, string? texto, List<FieldError> erros)
    {
        ArgumentNullException.ThrowIfNull(erros);

        int antes = erros.Count;
        bool parecerValido = PareceresDaManifestacao.TryAnalisar(parecer, out ParecerDaManifestacao lido);
        if (!parecerValido)
        {
            erros.Add(new("parecer", new DomainError(
                ProtecaoDoCatalogoErrorCodes.ParecerInvalido,
                $"O parecer deve ser {string.Join(", ", PareceresDaManifestacao.TokensCanonicos)}.")));
        }

        bool obrigatorio = parecerValido && PareceresDaManifestacao.EhDesfavoravel(lido);
        Fundamentacao? lida = Fundamentacao.Ler(texto, obrigatorio, RecusasDaFundamentacao.DoTextoDaManifestacao, erros);
        return erros.Count == antes ? new ConteudoDaManifestacao(lido, lida) : null;
    }
}
