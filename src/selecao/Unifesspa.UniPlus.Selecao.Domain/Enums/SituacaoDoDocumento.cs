namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// O que uma exigência documental é para o candidato, diante das respostas dele (UNI-REQ-0069,
/// UNI-REQ-0077).
/// </summary>
public enum SituacaoDoDocumento
{
    Nenhuma = 0,

    /// <summary>A exigência não se aplica ao candidato.</summary>
    NaoExigido = 1,

    /// <summary>A exigência se aplica ao candidato.</summary>
    Exigido = 2,

    /// <summary>Uma resposta que ainda falta decide se a exigência se aplica.</summary>
    PodeSerExigido = 3,

    /// <summary>A exigência depende de um fato que só se conhece numa fase posterior à da inscrição.</summary>
    DependeDeFasePosterior = 4,
}
