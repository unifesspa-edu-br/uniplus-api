namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using Entities;

/// <summary>
/// O resultado de uma reconciliação de produtos: a lista final na ordem de chegada e, para cada
/// produto que chegou, o <c>Id</c> da linha que ficou no seu lugar, a linha viva reaproveitada ou o
/// próprio produto que chegou quando entrou como novo. É o mapa que a reancoragem consulta para
/// apontar o recurso ao <c>Id</c> que permanece.
/// </summary>
internal sealed record ProdutosReconciliados<T>(
    IReadOnlyList<T> Produtos,
    IReadOnlyDictionary<Guid, Guid> IdentidadeVivaPorIdentidadeChegada)
    where T : IProdutoPublicado;
