namespace Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// Quantos valores o fato assume para um mesmo candidato. É o eixo que decide em que sentido o operador se lê: sobre um
/// único valor, <see cref="Operador.Igual"/> compara; sobre um conjunto, significa que o conjunto inclui o valor.
/// </summary>
public enum CardinalidadeDoFato
{
    /// <summary>O candidato tem um único valor para o fato.</summary>
    ValorUnico = 1,

    /// <summary>O candidato pode ter mais de um valor para o fato; só o fato categórico é assim.</summary>
    Conjunto = 2,
}
