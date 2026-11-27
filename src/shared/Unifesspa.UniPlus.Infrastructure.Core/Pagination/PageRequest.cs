namespace Unifesspa.UniPlus.Infrastructure.Core.Pagination;

using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Parâmetros decodificados de um request paginado: <c>Id</c> de continuação
/// (<see cref="AfterId"/>, ausente na primeira página), tamanho efetivo da
/// página (<see cref="Limit"/>), direção de navegação
/// (<see cref="Direction"/>, ADR-0089) e a chave de
/// continuação (<see cref="AfterSortKey"/>, ADR-0094). Como esses valores chegam via wire é
/// responsabilidade do binder configurado por atributo (ex.:
/// <c>[FromCursor]</c> para cursor opaco AES-GCM, ADR-0026); outros
/// mecanismos podem ser adicionados sem afetar o consumo.
/// </summary>
/// <param name="AfterSortKey">
/// Chave de continuação (ADR-0094); a continuação é <c>(AfterSortKey, AfterId)</c>. <c>null</c> só na
/// primeira página.
/// </param>
public sealed record PageRequest(
    Guid? AfterId,
    int Limit,
    PaginationDirection Direction,
    string? AfterSortKey = null);
