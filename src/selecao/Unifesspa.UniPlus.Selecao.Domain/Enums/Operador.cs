namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Operador de uma <see cref="ValueObjects.CondicaoDnf"/> — a tripla
/// <c>{ Fato, Operador, Valor }</c> que compõe a forma normal disjuntiva de um
/// predicado sobre o candidato (ADR-0111, Story #847; operadores de exclusão
/// <see cref="Diferente"/>/<see cref="NaoEm"/>, Story #916). Conjunto fechado por
/// design: os seis operadores espelham exatamente a matriz operador ×
/// domínio da ADR-0111.
/// </summary>
public enum Operador
{
    Nenhuma = 0,

    /// <summary>Igualdade — único operador aceito para domínio booleano; um dos dois para categórico.</summary>
    Igual = 1,

    /// <summary>Pertencimento a uma lista — só aceito para domínio categórico.</summary>
    Em = 2,

    /// <summary>Maior ou igual — só aceito para domínio numérico.</summary>
    MaiorIgual = 3,

    /// <summary>Menor ou igual — só aceito para domínio numérico.</summary>
    MenorIgual = 4,

    /// <summary>Negação de <see cref="Igual"/> (Story #916) — aceito em todo domínio onde <see cref="Igual"/> vale.</summary>
    Diferente = 5,

    /// <summary>Negação de <see cref="Em"/> (Story #916) — só aceito onde <see cref="Em"/> vale.</summary>
    NaoEm = 6,
}
