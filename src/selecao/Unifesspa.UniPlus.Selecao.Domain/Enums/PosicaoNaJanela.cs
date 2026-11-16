namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Onde um instante está em relação a uma janela de tempo. O extremo ausente da janela não limita.
/// </summary>
public enum PosicaoNaJanela
{
    /// <summary>O instante é anterior ao início da janela.</summary>
    Antes = 1,

    /// <summary>O instante está dentro da janela, inclusive nos extremos.</summary>
    Dentro = 2,

    /// <summary>O instante é posterior ao fim da janela.</summary>
    Depois = 3,
}
