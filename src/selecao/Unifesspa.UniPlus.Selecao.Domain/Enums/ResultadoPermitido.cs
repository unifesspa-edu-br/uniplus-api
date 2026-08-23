namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Resultado que um motivo de decisão de isenção pode fundamentar
/// (UNI-REQ-0121). Cada motivo pertence a exatamente um deles: o vocabulário é
/// fechado e não admite motivo que sirva às duas conclusões.
/// </summary>
/// <remarks>
/// <see cref="Nenhum"/> é a sentinela de "não informado", e não um terceiro
/// resultado — existe para que a ausência no wire chegue ao domínio como
/// ausência, em vez de virar silenciosamente o primeiro valor do enum.
/// </remarks>
public enum ResultadoPermitido
{
    Nenhum = 0,
    Deferido = 1,
    Indeferido = 2,
}
