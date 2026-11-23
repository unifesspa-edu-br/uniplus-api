namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

/// <summary>
/// Documentos dos quais o candidato apresenta ao menos a <paramref name="QuantidadeMinima"/>: quantidade
/// ausente vale um. O grupo tem um nível só, sem outro grupo dentro. Cada documento do grupo é do mesmo
/// formato do documento avulso do conjunto.
/// </summary>
/// <param name="QuantidadeMinima">Quantos documentos do grupo o candidato apresenta; ausente vale um.</param>
/// <param name="Indeferimento">A consequência de a quantidade não ser atingida e as bases legais dela; ausente, o grupo não determina resultado.</param>
/// <param name="Documentos">Os documentos do grupo.</param>
public sealed record GrupoDeDocumentosDoModelo(
    int? QuantidadeMinima,
    IndeferimentoDoGrupoDeDocumentosDoModelo? Indeferimento,
    IReadOnlyList<DocumentoExigidoDoModelo> Documentos);
