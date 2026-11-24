namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// O que uma exigência documental é para o candidato, diante das respostas dele (UNI-REQ-0069,
/// UNI-REQ-0077).
/// </summary>
public enum SituacaoDoDocumento
{

    /// <summary>A exigência não se aplica ao candidato.</summary>
    [CodigoDeContrato("NAO_EXIGIDO", "Não exigido", Descricao = "A exigência não se aplica.")]
    NaoExigido = 1,

    /// <summary>A exigência se aplica ao candidato e ainda não foi atendida pelos documentos enviados.</summary>
    [CodigoDeContrato("PENDENTE", "Pendente", Descricao = "A exigência ainda não foi atendida.")]
    Pendente = 2,

    /// <summary>Uma resposta que ainda falta decide se a exigência se aplica.</summary>
    [CodigoDeContrato("PODE_SER_EXIGIDO", "Pode ser exigido", Descricao = "Uma resposta pendente decide a exigência.")]
    PodeSerExigido = 3,

    /// <summary>A exigência depende de um fato que só se conhece numa fase posterior à da inscrição.</summary>
    [CodigoDeContrato("DEPENDE_DE_FASE_POSTERIOR", "Depende de fase posterior", Descricao = "A exigência depende de uma fase posterior.")]
    DependeDeFasePosterior = 4,

    /// <summary>A exigência se aplica ao candidato e os documentos enviados a atendem.</summary>
    [CodigoDeContrato("SATISFEITA", "Satisfeita", Descricao = "A exigência foi atendida.")]
    Satisfeita = 5,
}
