namespace Unifesspa.UniPlus.Configuracao.Application.Queries.FasesCanonicas;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Lista fases canônicas vivas paginadas por cursor bidirecional (ADR-0026 +
/// ADR-0089). O controller decifra o cursor opaco e valida limit/direction antes
/// de despachar.
/// </summary>
public sealed record ListarFasesCanonicasQuery(
    Guid? AfterId,
    int Limit,
    PaginationDirection Direction) : IQuery<ListarFasesCanonicasResult>;
