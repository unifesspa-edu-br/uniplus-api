namespace Unifesspa.UniPlus.Configuracao.Application.Queries.Cursos;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Lista cursos vivos paginados por cursor bidirecional (ADR-0026 + ADR-0089),
/// em ordem alfabética de nome. O controller decifra o cursor opaco e valida
/// limit/direction antes de despachar.
/// </summary>
/// <param name="AfterSortKey">
/// Chave de ordenação da âncora de continuação (ADR-0094), que acompanha o
/// <paramref name="AfterId"/>; <c>null</c> na primeira página.
/// </param>
public sealed record ListarCursosQuery(
    string? AfterSortKey,
    Guid? AfterId,
    int Limit,
    PaginationDirection Direction) : IQuery<ListarCursosResult>;
