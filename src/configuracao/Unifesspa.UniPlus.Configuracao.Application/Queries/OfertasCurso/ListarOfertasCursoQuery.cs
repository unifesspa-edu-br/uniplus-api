namespace Unifesspa.UniPlus.Configuracao.Application.Queries.OfertasCurso;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Lista ofertas de curso vivas paginadas por cursor bidirecional
/// (ADR-0026 + ADR-0089). O controller decifra o cursor opaco e valida
/// limit/direction antes de despachar.
/// </summary>
public sealed record ListarOfertasCursoQuery(
    Guid? AfterId,
    int Limit,
    PaginationDirection Direction) : IQuery<ListarOfertasCursoResult>;
