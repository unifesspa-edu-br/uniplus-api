namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// O que acontece quando os corretores não chegam ao conceito da etapa pelo critério da regra (falta de
/// unanimidade ou empate). É declarado pela versão da regra de consolidação; o conjunto é fechado e cresce por
/// emenda da ADR-0150.
/// </summary>
public enum TratamentoDaFaltaDeConsolidacao
{
    /// <summary>Um corretor adicional atribui o conceito, e a apuração se repete com a atribuição dele.</summary>
    NovoCorretor = 1,

    /// <summary>A banca decide o conceito da etapa.</summary>
    DecisaoDaBanca = 2,
}
