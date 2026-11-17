namespace Unifesspa.UniPlus.Publicacoes.Domain.ValueObjects;

using System.Globalization;

/// <summary>
/// A numeração que o órgão deu ao ato: a série (órgão, ano) atravessa certames, e o número é
/// declarado, opcional e sem unicidade imposta (ADR-0105). É a chave do aviso de número duplicado.
/// </summary>
public readonly record struct NumeracaoDoAto(string Orgao, string Serie, int Ano, string? Numero)
{
    /// <summary>
    /// Como a recusa nomeia o ato que a causa: pela numeração oficial (série, número e ano), que o
    /// operador reconhece, e nunca pelo identificador.
    /// </summary>
    public string Descrever() =>
        Numero is null
            ? string.Create(CultureInfo.InvariantCulture, $"{Serie} sem número de {Ano}")
            : string.Create(CultureInfo.InvariantCulture, $"{Serie} nº {Numero}/{Ano}");
}
