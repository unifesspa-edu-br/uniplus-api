namespace Unifesspa.UniPlus.Regras.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Operador de uma <see cref="Regras.ValueObjects.CondicaoDnf"/> — a tripla
/// <c>{ Fato, Operador, Valor }</c> que compõe a forma normal disjuntiva de um
/// predicado sobre o candidato (ADR-0111, Story #847; operadores de exclusão
/// <see cref="Diferente"/>/<see cref="NaoEm"/>, Story #916). Conjunto fechado por
/// design: os seis operadores espelham exatamente a matriz operador ×
/// domínio da ADR-0111.
/// </summary>
/// <remarks>
/// A identidade persistida do operador é o código declarado (ADR-0176). A numeração dos
/// membros reflete a ordem em que os operadores foram introduzidos: não é gravada nem é peso
/// de ordenação. A ordem canônica que entra no hash de congelamento é a ordinal do código
/// textual, e decorre de a canonicalização ordenar cada átomo pela sua própria serialização
/// canônica; ordenar por valor numérico produziria outra ordem.
/// </remarks>
[DescricaoDoContrato("Operador que compara o fato do candidato com o valor da condição. Sobre fato multivalorado, igual a significa pertencer ao conjunto do candidato (ADR-0111, Emenda 1.2).")]
public enum Operador
{
    /// <summary>Igualdade — aceito nos três domínios.</summary>
    [CodigoDeContrato("IGUAL", "é igual a")]
    [RotuloSobreConjunto("inclui")]
    Igual = 1,

    /// <summary>Pertencimento a uma lista — só aceito para domínio categórico.</summary>
    [CodigoDeContrato("EM", "é um de")]
    [RotuloSobreConjunto("inclui algum de")]
    Em = 2,

    /// <summary>Maior ou igual — só aceito para domínio numérico.</summary>
    [CodigoDeContrato("MAIOR_IGUAL", "maior ou igual a")]
    MaiorIgual = 3,

    /// <summary>Menor ou igual — só aceito para domínio numérico.</summary>
    [CodigoDeContrato("MENOR_IGUAL", "menor ou igual a")]
    MenorIgual = 4,

    /// <summary>
    /// Negação de <see cref="Igual"/> (Story #916) — aceito em todo domínio onde
    /// <see cref="Igual"/> vale. Sobre fato multivalorado significa que nenhum elemento do
    /// conjunto é igual ao valor configurado.
    /// </summary>
    [CodigoDeContrato("DIFERENTE", "é diferente de")]
    [RotuloSobreConjunto("não inclui")]
    Diferente = 5,

    /// <summary>
    /// Negação de <see cref="Em"/> (Story #916) — só aceito onde <see cref="Em"/> vale.
    /// Sobre fato multivalorado significa interseção vazia com a lista configurada.
    /// </summary>
    [CodigoDeContrato("NAO_EM", "não é nenhum de")]
    [RotuloSobreConjunto("não inclui nenhum de")]
    NaoEm = 6,
}
