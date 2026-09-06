namespace Unifesspa.UniPlus.Configuracao.Application.Queries.OfertasCurso;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Lista ofertas de curso vivas paginadas por cursor bidirecional
/// (ADR-0026 + ADR-0089), em ordem alfabética pelo nome do curso ofertado. O
/// controller decifra o cursor opaco e valida limit/direction antes de despachar.
/// </summary>
/// <param name="AfterSortKey">
/// Chave de ordenação da âncora de continuação (ADR-0094), que acompanha o
/// <paramref name="AfterId"/>; <c>null</c> na primeira página.
/// </param>
/// <param name="AfterId">Âncora da página anterior; <c>null</c> retorna a primeira janela.</param>
/// <param name="Limit">Tamanho máximo da página a retornar.</param>
/// <param name="Direction">Direção de navegação (<c>Next</c>/<c>Prev</c>, ADR-0089).</param>
/// <param name="CursoId">
/// Filtro opcional (issue #755): restringe às ofertas do curso informado;
/// <c>null</c> = sem filtro. Combina com o cursor — o filtro é aplicado à query
/// antes do keyset, então itens e âncoras <c>prev</c>/<c>next</c> respeitam o recorte.
/// </param>
/// <param name="Ordenacao">
/// Campos de ordenação pedidos, na ordem de prioridade; vazio usa a ordem padrão.
/// </param>
/// <param name="Busca">Texto pesquisado; <c>null</c> ou em branco lista tudo.</param>
public sealed record ListarOfertasCursoQuery(
    IReadOnlyList<SortField> Ordenacao,
    string? Busca,
    string? AfterSortKey,
    Guid? AfterId,
    int Limit,
    PaginationDirection Direction,
    Guid? CursoId) : IQuery<Result<ListarOfertasCursoResult>>;
