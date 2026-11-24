namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Se o motivo é favorável ou desfavorável à pessoa (UNI-REQ-0283). Cada motivo tem
/// exatamente um sentido, e cada decisão o traduz no próprio rótulo: deferido e indeferido na
/// isenção, homologada e não homologada na homologação das inscrições.
/// </summary>
public enum SentidoDoMotivo
{
    [CodigoDeContrato("FAVORAVEL", "favorável", Descricao = "O motivo é favorável à pessoa.")]
    Favoravel = 1,
    [CodigoDeContrato("DESFAVORAVEL", "desfavorável", Descricao = "O motivo é desfavorável à pessoa.")]
    Desfavoravel = 2,
}
