namespace Unifesspa.UniPlus.Infrastructure.Core.Pagination;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Extensões de <see cref="ControllerBase"/> para responder coleções
/// paginadas com cursor (ADR-0026): encoda o cursor das páginas vizinhas,
/// monta o header <c>Link</c> (RFC 5988/8288) e o header <c>X-Page-Size</c>,
/// e devolve o body como array JSON puro (ADR-0025). Quem informa o total da
/// consulta ganha também <c>X-Total-Count</c>, que só aparece na resposta de
/// quem o pediu. A coleção, o vínculo ao usuário e a etiqueta do cursor vêm do
/// <see cref="FromCursorAttribute"/> da própria ação.
/// </summary>
public static class PaginationControllerExtensions
{
    /// <summary>
    /// Devolve <see cref="StatusCodes.Status200OK"/> com os <paramref name="items"/> como array JSON; encoda o cursor
    /// de cada lado em que há página (<paramref name="previous"/> e <paramref name="next"/> não nulos) e popula
    /// <c>Link</c> e <c>X-Page-Size</c>. Cada ponto de continuação carrega a chave de continuação <strong>e</strong> o
    /// <c>Id</c> de desempate, serializados no payload opaco.
    /// </summary>
    /// <param name="total">
    /// Total de registros da consulta que produziu a página, quando o endpoint oferece
    /// a contagem e o cliente a pediu. Responde em <c>X-Total-Count</c>; <c>null</c>
    /// não emite o header (ADR-0026).
    /// </param>
    public static Task<IActionResult> OkPaginatedAsync<T>(
        this ControllerBase controller,
        IReadOnlyList<T> items,
        (string SortKey, Guid Id)? previous,
        (string SortKey, Guid Id)? next,
        PageRequest page,
        int? total = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);

        PageResponseWriter writer = PageResponseWriter.For(controller, page.Limit);
        if (total is { } contagem)
            writer.WriteTotalCount(contagem);

        return writer.WriteAsync(items, PontoDeContinuacao.De(previous), PontoDeContinuacao.De(next), cancellationToken);
    }
}
