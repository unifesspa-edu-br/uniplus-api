namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Lista processos seletivos paginados por cursor bidirecional (ADR-0026 +
/// ADR-0089), espelhando <c>ListarEditaisQuery</c>.
/// </summary>
public sealed record ListarProcessosSeletivosQuery(
    Guid? AfterId,
    int Limit,
    PaginationDirection Direction) : IQuery<ListarProcessosSeletivosResult>;
