namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Veredicto de um item do checklist de conformidade ESTRUTURAL do <c>ProcessoSeletivo</c>
/// (<see cref="Entities.ProcessoSeletivo.AvaliarConformidade"/>, Story #758 CA-07, Story #759
/// CA-03, issue #1092). Tipo de Domain — a versão Application-facing (<c>ItemConformidadeDto</c>)
/// é mapeada a partir deste no query handler, para que o checklist usado pela leitura pública e
/// os quatro gates de <c>Publicar</c>/<c>SucederVersao</c> nunca divirjam: <c>AvaliarConformidade</c>
/// projeta um item por razão de recusa ALCANÇÁVEL nos quatro gates, a partir dos MESMOS predicados
/// que os gates chamam — nunca uma segunda lista copiada.
/// </summary>
/// <remarks>
/// <b>"Estrutural" não é "publicável".</b> Todo item <see langword="true"/> significa que o
/// agregado está estruturalmente completo e coerente — não que a publicação vai necessariamente
/// aceitar. Conformidade legal, documento confirmado e tipo de ato são avaliados à parte
/// (<c>GET /conformidade-legal</c> e o command handler de publicação), nunca aqui.
/// </remarks>
public sealed record ItemConformidade(string Item, bool Ok);
