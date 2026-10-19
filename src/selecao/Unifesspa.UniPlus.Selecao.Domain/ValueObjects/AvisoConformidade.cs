namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Aviso da conformidade do <c>ProcessoSeletivo</c>: algo que o administrador deve confirmar e que
/// <b>nunca impede a publicação</b>. Não é um <see cref="ItemConformidade"/>: o item reprovado
/// corresponde a uma recusa de gate, e o aviso não tem gate.
/// </summary>
/// <remarks>
/// A identidade do aviso é o par código-dimensão, como no item: a mensagem é redação e pode mudar.
/// O código pertence ao conjunto fechado de <see cref="CodigosDeAvisoDeConformidade"/>.
/// </remarks>
/// <param name="Codigo">Identificador estável do aviso — um valor de <see cref="CodigosDeAvisoDeConformidade"/>.</param>
/// <param name="Dimensao">Seção do agregado onde o administrador confere — um valor de <see cref="DimensaoConformidade"/>.</param>
/// <param name="Mensagem">Texto humano do aviso. Pode mudar sem que código ou dimensão mudem.</param>
public sealed record AvisoConformidade(string Codigo, string Dimensao, string Mensagem);
