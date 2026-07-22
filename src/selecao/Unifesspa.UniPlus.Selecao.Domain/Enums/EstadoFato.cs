namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Estado de resolução de um fato do candidato (Story #926, ADR-0111). É o
/// discriminador que distingue "não se aplica a este candidato" de "ainda não se
/// sabe" — distinção que o valor sozinho nunca carrega, porque ambos os casos
/// chegariam como ausência de valor.
/// </summary>
/// <remarks>
/// <para>
/// A distinção é material: <see cref="NaoAplicavel"/> é uma resposta
/// <b>definitiva</b> — a pré-condição do campo é falsa, o campo nem foi
/// apresentado ao candidato, e uma exigência que dependa dele fica dispensada.
/// <see cref="Indeterminado"/> é <b>pendência</b> — o campo se aplica, o
/// candidato ainda não respondeu, e a exigência permanece em aberto. Colapsar os
/// dois dispensaria exigência sobre a qual nada se sabe.
/// </para>
/// <para>
/// <see cref="Indeterminado"/> é <c>0</c> pelo mesmo motivo de
/// <see cref="Ternario"/>: o zero-default de C# tem de ser o estado fail-closed.
/// Um campo esquecido nunca é lido como resolvido nem como inaplicável.
/// </para>
/// </remarks>
public enum EstadoFato
{
    /// <summary>O fato se aplica, mas o seu valor ainda não é conhecido — pendência.</summary>
    Indeterminado = 0,

    /// <summary>
    /// O fato não se aplica a este candidato porque a sua pré-condição é falsa.
    /// É estado <b>resolvido</b>, não pendência: não há valor a esperar.
    /// </summary>
    NaoAplicavel = 1,

    /// <summary>O fato se aplica e tem valor conhecido.</summary>
    Resolvido = 2,
}
