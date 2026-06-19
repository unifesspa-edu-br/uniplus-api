namespace Unifesspa.UniPlus.Infrastructure.Core.Pagination;

using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Parâmetros decodificados de um request paginado: chave de continuação
/// (<see cref="AfterId"/>, ausente na primeira página), tamanho efetivo da
/// janela (<see cref="Limit"/>), direção de navegação
/// (<see cref="Direction"/>, ADR-0089) e, em recursos com ordenação keyset
/// multi-coluna (ADR-0094), a chave de ordenação da âncora
/// (<see cref="AfterSortKey"/>). Como esses valores chegam via wire é
/// responsabilidade do binder configurado por atributo (ex.:
/// <c>[FromCursor]</c> para cursor opaco AES-GCM, ADR-0026); outros
/// mecanismos podem ser adicionados sem afetar o consumo.
/// </summary>
/// <param name="AfterSortKey">
/// Chave de ordenação da âncora para keyset ordenado (ADR-0094); a continuação é
/// <c>(AfterSortKey, AfterId)</c>. <c>null</c> nos recursos paginados por <c>Id</c>
/// (a maioria), que ignoram este campo.
/// </param>
public sealed record PageRequest(
    Guid? AfterId,
    int Limit,
    PaginationDirection Direction,
    string? AfterSortKey = null);
