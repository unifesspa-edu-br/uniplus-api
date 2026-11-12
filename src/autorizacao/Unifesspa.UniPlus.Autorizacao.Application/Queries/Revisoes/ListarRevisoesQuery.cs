namespace Unifesspa.UniPlus.Autorizacao.Application.Queries.Revisoes;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Lista as revisões paginadas por cursor opaco bidirecional (ADR-0026 + ADR-0089).</summary>
/// <param name="Situacao">A situação pedida; sem ela, as revisões em qualquer situação.</param>
public sealed record ListarRevisoesQuery(Guid? AfterId, int Limit, PaginationDirection Direction, SituacaoDaRevisao? Situacao = null)
    : IQuery<Result<ListarRevisoesResult>>;

/// <summary>Resultado paginado: as âncoras são os <c>Id</c> de fronteira da janela.</summary>
public sealed record ListarRevisoesResult(
    IReadOnlyList<RevisaoResumoDto> Items,
    Guid? AnteriorAfterId,
    Guid? ProximoAfterId);
