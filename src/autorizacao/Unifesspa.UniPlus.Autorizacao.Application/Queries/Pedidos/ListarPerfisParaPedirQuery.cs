namespace Unifesspa.UniPlus.Autorizacao.Application.Queries.Pedidos;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Lista os perfis ativos que se podem pedir, paginados por cursor opaco bidirecional (ADR-0026 + ADR-0089).</summary>
public sealed record ListarPerfisParaPedirQuery(
    Guid? AfterId,
    int Limit,
    PaginationDirection Direction) : IQuery<Result<ListarPerfisParaPedirResult>>;

/// <summary>Resultado paginado: as âncoras são os <c>Id</c> de fronteira da janela.</summary>
public sealed record ListarPerfisParaPedirResult(
    IReadOnlyList<PerfilParaPedidoDto> Items,
    Guid? AnteriorAfterId,
    Guid? ProximoAfterId);
