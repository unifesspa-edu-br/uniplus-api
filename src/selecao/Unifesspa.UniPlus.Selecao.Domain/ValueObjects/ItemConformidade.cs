namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Veredicto de um item do checklist de conformidade ESTRUTURAL do <c>ProcessoSeletivo</c>
/// (<see cref="ProcessosSeletivos.Conformidade.ChecklistDeConformidade.AvaliarConformidade"/>). Tipo de Domain: a
/// versão Application-facing (<c>ItemConformidadeDto</c>) é mapeada a partir deste no query handler. O
/// checklist e os gates de <c>TransicoesDaVersao.Publicar</c>, <c>Retificar</c> e <c>Fechar</c> nunca
/// divergem: o checklist projeta um item por razão de recusa alcançável nos gates, a partir dos mesmos
/// predicados que eles chamam, nunca uma segunda lista copiada.
/// </summary>
/// <remarks>
/// <para>
/// <b>"Estrutural" não é "publicável".</b> Todo item <see langword="true"/> significa que o
/// agregado está estruturalmente completo e coerente — não que a publicação vai necessariamente
/// aceitar. Conformidade legal, documento confirmado e tipo de ato são avaliados à parte
/// (<c>GET /conformidade-legal</c> e o command handler de publicação), nunca aqui.
/// </para>
/// <para>
/// <b>A identidade do item é o par código-dimensão, nunca a mensagem.</b> A mensagem é
/// redação, e redação melhora: quem tiver ligado uma decisão de cliente ao texto vê a decisão
/// quebrar na primeira revisão editorial. O código é estável e distingue invariantes dentro
/// da mesma dimensão; a dimensão diz em que seção do agregado a correção acontece.
/// </para>
/// </remarks>
/// <param name="Codigo">Identificador estável da invariante, único no checklist.</param>
/// <param name="Dimensao">Seção do agregado onde a pendência se corrige — um valor de <see cref="DimensaoConformidade"/>.</param>
/// <param name="Mensagem">Texto humano do item. Pode mudar sem que código ou dimensão mudem.</param>
/// <param name="Ok">Veredicto do item no estado atual do agregado.</param>
public sealed record ItemConformidade(string Codigo, string Dimensao, string Mensagem, bool Ok);
