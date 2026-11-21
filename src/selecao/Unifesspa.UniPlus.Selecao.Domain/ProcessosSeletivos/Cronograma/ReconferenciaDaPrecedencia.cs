namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// Quando a operação confere de novo a precedência do cadastro: na primeira publicação e sempre que o cronograma mudou desde a
/// versão publicada. O cadastro não é consultado depois da publicação (ADR-0113): a retificação que não toca o cronograma não é
/// recusada por precedência cadastrada depois. Conta o que a conferência lê (as fases presentes, a ordem, o início e o fim de cada
/// uma); banca, ato e demais partes da fase não contam.
/// </summary>
public static class ReconferenciaDaPrecedencia
{
    /// <summary>Se o cronograma atual precisa ser conferido contra o cadastro, dada a versão publicada; sem versão, sempre.</summary>
    public static bool Exigida(IReadOnlyCollection<FaseCronograma>? fasesDaVersaoPublicada, IReadOnlyCollection<FaseCronograma> fasesAtuais)
    {
        ArgumentNullException.ThrowIfNull(fasesAtuais);

        return fasesDaVersaoPublicada is null || !FasesComoAConferenciaAsLe(fasesDaVersaoPublicada).SequenceEqual(FasesComoAConferenciaAsLe(fasesAtuais));
    }

    private static IEnumerable<FaseLidaPelaConferencia> FasesComoAConferenciaAsLe(IEnumerable<FaseCronograma> fases) =>
        fases
            .Select(static fase => new FaseLidaPelaConferencia(fase.FaseCanonicaOrigemId, fase.Codigo, fase.Ordem, fase.Inicio, fase.Fim))
            .OrderBy(static fase => fase.OrigemId);

    private sealed record FaseLidaPelaConferencia(
        Guid OrigemId, string Codigo, int Ordem, DateTimeOffset? Inicio, DateTimeOffset? Fim);
}
