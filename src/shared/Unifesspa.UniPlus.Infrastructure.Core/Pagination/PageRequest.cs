namespace Unifesspa.UniPlus.Infrastructure.Core.Pagination;

/// <summary>
/// Parâmetros decodificados de um request paginado: chave de continuação
/// (<see cref="AfterId"/>, ausente na primeira página) e tamanho efetivo da
/// janela (<see cref="Limit"/>). Como esses valores chegam via wire é
/// responsabilidade do binder configurado por atributo (ex.:
/// <c>[FromCursor]</c> para cursor opaco AES-GCM, ADR-0026); outros
/// mecanismos podem ser adicionados sem afetar o consumo.
/// </summary>
public sealed record PageRequest(Guid? AfterId, int Limit);
