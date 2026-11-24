namespace Unifesspa.UniPlus.Regras.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Operador de uma <see cref="Regras.ValueObjects.CondicaoDnf"/> — a tripla
/// <c>{ Fato, Operador, Valor }</c> que compõe a forma normal disjuntiva de um
/// predicado sobre o candidato (ADR-0111; operadores de exclusão
/// <see cref="Diferente"/>/<see cref="NenhumDe"/>). Conjunto fechado por
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
[DescricaoDoContrato("Operador que compara o dado do candidato com o valor da condição. Sobre dado com vários valores, igual a significa pertencer ao conjunto do candidato (ADR-0111, Emenda 1.2).")]
public enum Operador
{
    /// <summary>Igualdade — aceito nos três domínios.</summary>
    [CodigoDeContrato("IGUAL", "é igual a")]
    [RotuloSobreConjunto("inclui")]
    Igual = 1,

    /// <summary>Pertencimento a uma lista — só aceito para domínio categórico.</summary>
    [CodigoDeContrato("ALGUM_DE", "é um de")]
    [RotuloSobreConjunto("inclui algum de")]
    AlgumDe = 2,

    /// <summary>Maior ou igual — só aceito para domínio numérico.</summary>
    [CodigoDeContrato("MAIOR_IGUAL", "maior ou igual a")]
    MaiorIgual = 3,

    /// <summary>Menor ou igual — só aceito para domínio numérico.</summary>
    [CodigoDeContrato("MENOR_IGUAL", "menor ou igual a")]
    MenorIgual = 4,

    /// <summary>
    /// Negação de <see cref="Igual"/> — aceito em todo domínio onde
    /// <see cref="Igual"/> vale. Sobre dado com vários valores significa que nenhum elemento do
    /// conjunto é igual ao valor configurado.
    /// </summary>
    [CodigoDeContrato("DIFERENTE", "é diferente de")]
    [RotuloSobreConjunto("não inclui")]
    Diferente = 5,

    /// <summary>
    /// Negação de <see cref="AlgumDe"/> — só aceito onde <see cref="AlgumDe"/> vale.
    /// Sobre dado com vários valores significa interseção vazia com a lista configurada.
    /// </summary>
    [CodigoDeContrato("NENHUM_DE", "não é nenhum de")]
    [RotuloSobreConjunto("não inclui nenhum de")]
    NenhumDe = 6,
}
