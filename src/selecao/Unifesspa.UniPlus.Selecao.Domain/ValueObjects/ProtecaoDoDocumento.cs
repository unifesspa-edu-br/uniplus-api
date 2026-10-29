namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// A proteção que o tipo de documento declarava quando a exigência o copiou (snapshot-copy,
/// ADR-0061): classificação na escala única, finalidade do tratamento e hipótese legal (token
/// canônico UPPER_SNAKE do vocabulário da Configuração, que este Domain não alcança, ADR-0097).
/// Mudar o tipo depois não altera a exigência que já o copiou.
/// </summary>
public sealed record ProtecaoDoDocumento(
    ClassificacaoDeProtecao Classificacao, string Finalidade, string HipoteseLegal);
