namespace Unifesspa.UniPlus.Infrastructure.Core.Pagination;

using System.Globalization;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Escreve a resposta de uma página (ADR-0026): emite os cursores anterior e seguinte, preenche
/// <c>Link</c>, <c>X-Page-Size</c> e <c>X-Total-Count</c> e devolve o corpo como array JSON puro. A coleção, o
/// vínculo ao usuário e a ordenação obrigatória vêm do <see cref="FromCursorAttribute"/> da própria ação, o
/// mesmo que o <see cref="PageRequestModelBinder"/> usa para conferir o cursor na volta. Vive uma requisição.
/// </summary>
internal sealed class PageResponseWriter
{
    private const string LinkHeader = "Link";
    private const string PageSizeHeader = "X-Page-Size";
    private const string TotalCountHeader = "X-Total-Count";

    private readonly ControllerBase _controller;
    private readonly FromCursorAttribute _declaration;
    private readonly CursorEncoder _encoder;
    private readonly int _limit;
    private readonly DateTimeOffset _expiresAt;

    private PageResponseWriter(
        ControllerBase controller,
        FromCursorAttribute declaration,
        CursorEncoder encoder,
        int limit,
        DateTimeOffset expiresAt)
    {
        _controller = controller;
        _declaration = declaration;
        _encoder = encoder;
        _limit = limit;
        _expiresAt = expiresAt;
    }

    /// <summary>
    /// Resolve, nesta ordem, a declaração do cursor da ação (que lança se faltar), o <see cref="CursorEncoder"/>,
    /// as opções de paginação e o <see cref="TimeProvider"/>.
    /// </summary>
    public static PageResponseWriter For(ControllerBase controller, int limit)
    {
        ArgumentNullException.ThrowIfNull(controller);

        FromCursorAttribute declaration = FromCursorAttribute.Of(controller.ControllerContext.ActionDescriptor)
            ?? throw new InvalidOperationException(
                $"A ação que devolve a página precisa declarar [FromCursor(\"<recurso>\")] no parâmetro {nameof(PageRequest)}.");

        IServiceProvider services = controller.HttpContext.RequestServices;
        CursorEncoder encoder = services.GetRequiredService<CursorEncoder>();
        CursorPaginationOptions options = services.GetRequiredService<IOptions<CursorPaginationOptions>>().Value;
        TimeProvider timeProvider = services.GetRequiredService<TimeProvider>();

        return new PageResponseWriter(controller, declaration, encoder, limit, timeProvider.GetUtcNow().Add(options.CursorTtl));
    }

    public void WriteTotalCount(int total) =>
        _controller.Response.Headers[TotalCountHeader] = total.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// O <see cref="IUserContext"/> só é resolvido quando há cursor a emitir: página única, sem navegação, não toca
    /// o DI, o que poupa os testes de fatia que registram só o <see cref="CursorEncoder"/> e o <see cref="TimeProvider"/>.
    /// </summary>
    public async Task<IActionResult> WriteAsync<T>(
        IReadOnlyList<T> items,
        PontoDeContinuacao? previous,
        PontoDeContinuacao? next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(items);

        string? userId = (previous is null && next is null) || !_declaration.RequireUserBinding ? null : UserIdForBinding();
        string? previousCursor = await EncodeAsync(previous, PaginationDirection.Prev, userId, cancellationToken).ConfigureAwait(false);
        string? nextCursor = await EncodeAsync(next, PaginationDirection.Next, userId, cancellationToken).ConfigureAwait(false);

        PageLinks links = PageLinks.ForRequest(_controller.Request, previousCursor, nextCursor);
        _controller.Response.Headers[LinkHeader] = LinkHeaderBuilder.Build(links);
        _controller.Response.Headers[PageSizeHeader] = items.Count.ToString(CultureInfo.InvariantCulture);
        return _controller.Ok(items);
    }

    /// <summary>
    /// Vínculo ao usuário (ADR-0026, "User-binding em cursores user-scoped"): a conta do principal corrente
    /// entra no cursor, e o binder confere a mesma conta na volta.
    /// </summary>
    private string UserIdForBinding()
    {
        IUserContext user = _controller.HttpContext.RequestServices.GetRequiredService<IUserContext>();
        if (!user.IsAuthenticated || string.IsNullOrEmpty(user.UserId))
        {
            throw new InvalidOperationException(
                "Cursor de [FromCursor(RequireUserBinding = true)] exige principal autenticado. " +
                "Verifique se o endpoint tem [Authorize] e se o middleware de auth está antes do MVC.");
        }

        return user.UserId;
    }

    /// <summary>
    /// O cursor vincula sua direção (ADR-0089): o do lado anterior cifra o ponto de continuação com <c>Prev</c> e o do seguinte, com
    /// <c>Next</c>, e o binder rejeita reuso com a direção trocada. <c>SortKey</c> só viaja no keyset ordenado (ADR-0094).
    /// </summary>
    private async Task<string?> EncodeAsync(PontoDeContinuacao? anchor, PaginationDirection direction, string? userId, CancellationToken cancellationToken)
    {
        if (anchor is not { } value)
        {
            return null;
        }

        string resourceTag = _declaration.ResourceTagFor(_controller.RouteData);
        CursorPayload payload = new(value.After, _limit, resourceTag, _expiresAt, direction, userId, value.SortKey);
        return await _encoder.EncodeAsync(payload, cancellationToken).ConfigureAwait(false);
    }
}
