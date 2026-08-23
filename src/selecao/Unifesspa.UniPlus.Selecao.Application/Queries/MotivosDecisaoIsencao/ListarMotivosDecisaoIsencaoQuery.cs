namespace Unifesspa.UniPlus.Selecao.Application.Queries.MotivosDecisaoIsencao;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Query paginada por cursor bidirecional (ADR-0026 + ADR-0089) do catálogo de
/// motivos, com filtro opcional por fundamento e por situação.
/// </summary>
public sealed record ListarMotivosDecisaoIsencaoQuery(
    Guid? AfterId,
    int Limit,
    PaginationDirection Direction,
    FundamentoIsencao? Fundamento,
    bool ApenasAtivos) : IQuery<ListarMotivosDecisaoIsencaoResult>;
