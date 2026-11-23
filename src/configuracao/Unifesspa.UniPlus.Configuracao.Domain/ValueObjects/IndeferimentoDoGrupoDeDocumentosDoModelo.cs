namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

/// <summary>
/// O que acontece quando o candidato não apresenta a quantidade mínima de documentos do grupo: a
/// consequência do indeferimento e as bases legais que a sustentam.
/// </summary>
public sealed record IndeferimentoDoGrupoDeDocumentosDoModelo(string Consequencia, IReadOnlyList<BaseLegalDoModelo> BasesLegais);
