namespace Unifesspa.UniPlus.Selecao.Application.Queries.MotivosDeDecisao;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Query paginada por cursor bidirecional (ADR-0026 + ADR-0089) do cadastro de motivos, com
/// filtro opcional por tipo, por subclassificação e por situação.
/// </summary>
public sealed record ListarMotivosDeDecisaoQuery(
    Guid? AfterId,
    int Limit,
    PaginationDirection Direction,
    FiltroDosMotivos Filtro) : IQuery<ListarMotivosDeDecisaoResult>;
