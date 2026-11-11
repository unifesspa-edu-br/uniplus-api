namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Se o motivo é favorável ou desfavorável à pessoa (UNI-REQ-0283). Cada motivo tem
/// exatamente um sentido, e cada decisão o traduz no próprio rótulo: deferido e indeferido na
/// isenção, homologada e não homologada na homologação das inscrições.
/// </summary>
/// <remarks><see cref="Nenhum"/> é a sentinela de "não informado", e não um terceiro sentido.</remarks>
public enum SentidoDoMotivo
{
    Nenhum = 0,
    Favoravel = 1,
    Desfavoravel = 2,
}
