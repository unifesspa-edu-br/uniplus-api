namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Entities;

/// <summary>
/// O que a fase publica, o parecer individual que essas publicações sustentam e o prazo de recurso
/// ancorado num dos produtos. É entrada de fábrica de vida curta, superficialmente imutável: carrega
/// a coleção somente leitura e as instâncias de <see cref="ProdutoDaFase"/> e
/// <see cref="RegraRecursoFase"/> que a fase passa a possuir, e nunca é guardada pela entidade.
/// </summary>
public sealed record PublicacoesDaFase(
    IReadOnlyList<ProdutoDaFase> Produtos,
    bool EmiteParecerIndividual,
    RegraRecursoFase? RegraRecurso);
