namespace Unifesspa.UniPlus.Configuracao.Application.Queries.Cursos;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Lista cursos vivos paginados por cursor bidirecional (ADR-0026 + ADR-0089).
/// O controller decifra o cursor opaco e valida limit/direction antes de
/// despachar.
/// </summary>
public sealed record ListarCursosQuery(
    Guid? AfterId,
    int Limit,
    PaginationDirection Direction) : IQuery<ListarCursosResult>;
