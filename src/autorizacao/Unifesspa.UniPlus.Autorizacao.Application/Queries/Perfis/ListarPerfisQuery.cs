namespace Unifesspa.UniPlus.Autorizacao.Application.Queries.Perfis;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>Lista os perfis paginados por cursor opaco bidirecional (ADR-0026 + ADR-0089).</summary>
public sealed record ListarPerfisQuery(
    Guid? AfterId,
    int Limit,
    PaginationDirection Direction) : IQuery<ListarPerfisResult>;

/// <summary>Resultado paginado: as âncoras são os <c>Id</c> de fronteira da janela.</summary>
public sealed record ListarPerfisResult(
    IReadOnlyList<PerfilDto> Items,
    Guid? AnteriorAfterId,
    Guid? ProximoAfterId);
