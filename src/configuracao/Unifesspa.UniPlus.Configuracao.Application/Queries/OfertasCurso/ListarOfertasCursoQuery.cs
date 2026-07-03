namespace Unifesspa.UniPlus.Configuracao.Application.Queries.OfertasCurso;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Lista ofertas de curso vivas paginadas por cursor bidirecional
/// (ADR-0026 + ADR-0089). O controller decifra o cursor opaco e valida
/// limit/direction antes de despachar.
/// </summary>
/// <param name="AfterId">Âncora da página anterior; <c>null</c> retorna a primeira janela.</param>
/// <param name="Limit">Tamanho máximo da página a retornar.</param>
/// <param name="Direction">Direção de navegação (<c>Next</c>/<c>Prev</c>, ADR-0089).</param>
/// <param name="CursoId">
/// Filtro opcional (issue #755): restringe às ofertas do curso informado;
/// <c>null</c> = sem filtro. Combina com o cursor — o filtro é aplicado à query
/// antes do keyset, então itens e âncoras <c>prev</c>/<c>next</c> respeitam o recorte.
/// </param>
public sealed record ListarOfertasCursoQuery(
    Guid? AfterId,
    int Limit,
    PaginationDirection Direction,
    Guid? CursoId) : IQuery<ListarOfertasCursoResult>;
