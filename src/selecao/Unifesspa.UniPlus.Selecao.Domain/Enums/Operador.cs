namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Operador de uma <see cref="ValueObjects.CondicaoDnf"/> — a tripla
/// <c>{ Fato, Operador, Valor }</c> que compõe a forma normal disjuntiva de um
/// predicado sobre o candidato (ADR-0111, Story #847; operadores de exclusão
/// <see cref="Diferente"/>/<see cref="NaoEm"/>, Story #916). Conjunto fechado por
/// design: os seis operadores espelham exatamente a matriz operador ×
/// domínio da ADR-0111.
/// </summary>
/// <remarks>
/// A numeração dos membros é identidade de persistência e reflete a ordem em que os
/// operadores foram introduzidos — não é peso de ordenação. A ordem canônica que entra
/// no hash de congelamento é a ordinal do código textual, e decorre de a canonicalização
/// ordenar cada átomo pela sua própria serialização canônica; ordenar por valor numérico
/// produziria outra ordem. O código textual de wire vive em
/// <see cref="OperadorCodigo"/>, nunca em <c>enum.ToString()</c>.
/// </remarks>
public enum Operador
{
    Nenhuma = 0,

    /// <summary>Igualdade — aceito nos três domínios.</summary>
    Igual = 1,

    /// <summary>Pertencimento a uma lista — só aceito para domínio categórico.</summary>
    Em = 2,

    /// <summary>Maior ou igual — só aceito para domínio numérico.</summary>
    MaiorIgual = 3,

    /// <summary>Menor ou igual — só aceito para domínio numérico.</summary>
    MenorIgual = 4,

    /// <summary>
    /// Negação de <see cref="Igual"/> (Story #916) — aceito em todo domínio onde
    /// <see cref="Igual"/> vale. Sobre fato multivalorado significa que nenhum elemento do
    /// conjunto é igual ao valor configurado.
    /// </summary>
    Diferente = 5,

    /// <summary>
    /// Negação de <see cref="Em"/> (Story #916) — só aceito onde <see cref="Em"/> vale.
    /// Sobre fato multivalorado significa interseção vazia com a lista configurada.
    /// </summary>
    NaoEm = 6,
}
