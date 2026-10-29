namespace Unifesspa.UniPlus.Autorizacao.Application.Queries.Pedidos;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Lista os pedidos paginados por cursor opaco bidirecional ordenado, do mais recente ao mais antigo (ADR-0026 + ADR-0094). A fila de cada aprovador é a lista
/// filtrada pela etapa que cabe a ele; <see cref="FilaDePedidos.Meus"/> lista os pedidos de quem pergunta.
/// </summary>
public sealed record ListarPedidosDeConcessaoQuery(
    FilaDePedidos Fila,
    SituacaoDoPedido? Situacao,
    string? AfterSortKey,
    Guid? AfterId,
    int Limit,
    PaginationDirection Direction) : IQuery<Result<ListarPedidosDeConcessaoResult>>;

/// <summary>Resultado paginado, do mais recente ao mais antigo: as âncoras são o par (instante, <c>Id</c>) de fronteira da janela.</summary>
public sealed record ListarPedidosDeConcessaoResult(
    IReadOnlyList<PedidoResumoDto> Items,
    (string SortKey, Guid Id)? Anterior,
    (string SortKey, Guid Id)? Proximo);
