namespace Unifesspa.UniPlus.Autorizacao.Application.Queries.Perfis;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Lista os perfis paginados por cursor opaco bidirecional (ADR-0026 + ADR-0089).</summary>
public sealed record ListarPerfisQuery(
    string? AfterSortKey,
    Guid? AfterId,
    int Limit,
    PaginationDirection Direction) : IQuery<Result<ListarPerfisResult>>;

/// <summary>Resultado paginado: os pontos de continuação são os <c>Id</c> de fronteira da página.</summary>
public sealed record ListarPerfisResult(
    IReadOnlyList<PerfilListadoDto> Items,
    (string SortKey, Guid Id)? Anterior,
    (string SortKey, Guid Id)? Proximo);
