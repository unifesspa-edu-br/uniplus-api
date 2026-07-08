namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Veredicto de um item obrigatório do checklist de conformidade estrutural
/// do <c>ProcessoSeletivo</c> (<see cref="Entities.ProcessoSeletivo.AvaliarConformidade"/>,
/// Story #758 CA-07 e Story #759 CA-03). Tipo de Domain — a versão
/// Application-facing (<c>ItemConformidadeDto</c>) é mapeada a partir deste
/// no query handler, para que o checklist usado pela leitura pública e o
/// gate de <c>Publicar</c> nunca divirjam.
/// </summary>
public sealed record ItemConformidade(string Item, bool Ok);
