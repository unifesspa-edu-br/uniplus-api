namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// A proteção que o tipo de documento declarava quando a exigência o copiou (snapshot-copy,
/// ADR-0061): classificação na escala única, finalidade do tratamento e hipótese legal (token
/// canônico UPPER_SNAKE do vocabulário da Configuração, que este Domain não alcança, ADR-0097).
/// Mudar o tipo depois não altera a exigência que já o copiou.
/// </summary>
public sealed record ProtecaoDoDocumento(
    ClassificacaoDeProtecao Classificacao, string Finalidade, string HipoteseLegal)
{
    /// <summary>O que falta à proteção para ser copiada; nulo quando está completa.</summary>
    internal string? Violacao() =>
        string.IsNullOrWhiteSpace(Finalidade) ? "A proteção do tipo de documento precisa de finalidade do tratamento."
        : string.IsNullOrWhiteSpace(HipoteseLegal) ? "A proteção do tipo de documento precisa de hipótese legal."
        : Classificacao == ClassificacaoDeProtecao.Nenhuma || !Enum.IsDefined(Classificacao) ? "A classificação de proteção do tipo de documento é obrigatória."
        : null;
}
