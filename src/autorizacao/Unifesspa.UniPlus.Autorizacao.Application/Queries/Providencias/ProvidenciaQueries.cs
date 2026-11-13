namespace Unifesspa.UniPlus.Autorizacao.Application.Queries.Providencias;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Lê uma providência, com o fundamento e o motivo do desfecho.</summary>
public sealed record ObterProvidenciaQuery(Guid Id) : IQuery<Result<ProvidenciaDto>>;

/// <summary>
/// A fila das providências, paginada por cursor opaco bidirecional (ADR-0026 + ADR-0089), em ordem de abertura, a mais
/// antiga primeiro.
/// </summary>
/// <param name="Situacao">A situação pedida; nula, todas.</param>
/// <param name="ConcessaoId">As providências de uma concessão, quando informada.</param>
/// <param name="AfterSortKey">A chave de ordenação da âncora, que viaja no cursor junto do <c>Id</c>.</param>
public sealed record ListarProvidenciasQuery(
    SituacaoDaProvidencia? Situacao, Guid? ConcessaoId, string? AfterSortKey, Guid? AfterId, int Limit, PaginationDirection Direction)
    : IQuery<ListarProvidenciasResult>;

/// <summary>Resultado paginado: as âncoras são os pares (chave de ordenação, <c>Id</c>) de fronteira da janela.</summary>
public sealed record ListarProvidenciasResult(
    IReadOnlyList<ProvidenciaResumoDto> Items, (string SortKey, Guid Id)? Anterior, (string SortKey, Guid Id)? Proximo);
