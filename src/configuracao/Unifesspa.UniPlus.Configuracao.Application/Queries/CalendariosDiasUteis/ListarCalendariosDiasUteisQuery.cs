namespace Unifesspa.UniPlus.Configuracao.Application.Queries.CalendariosDiasUteis;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Lista datasets de calendário de dias úteis vivos, paginados por cursor
/// bidirecional (ADR-0026 + ADR-0089). Sem os dias não úteis — use
/// <c>ObterCalendarioDiasUteisPorIdQuery</c> para o dataset completo.
/// </summary>
public sealed record ListarCalendariosDiasUteisQuery(
    Guid? AfterId,
    int Limit,
    PaginationDirection Direction) : IQuery<ListarCalendariosDiasUteisResult>;
