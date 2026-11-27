namespace Unifesspa.UniPlus.Infrastructure.Core.Pagination;

using System.Globalization;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Lê a página pedida numa requisição (<c>limit</c>, <c>direction</c> e <c>cursor</c>) e, quando
/// recusa, registra a recusa no contexto de binding. Vive uma requisição: quem o cria não o reaproveita.
/// </summary>
internal sealed class PageRequestReader
{
    private const string CursorParam = "cursor";
    private const string LimitParam = "limit";
    private const string DirectionParam = "direction";

    private readonly ModelBindingContext _context;
    private readonly FromCursorAttribute _attribute;
    private readonly CursorEncoder _encoder;
    private readonly CursorPaginationOptions _options;

    public PageRequestReader(
        ModelBindingContext context,
        FromCursorAttribute attribute,
        CursorEncoder encoder,
        CursorPaginationOptions options)
    {
        _context = context;
        _attribute = attribute;
        _encoder = encoder;
        _options = options;
    }

    /// <summary>
    /// Devolve a página pedida, ou <c>null</c> quando já registrou a recusa no contexto. A ordem das
    /// recusas é: limite, direção, decodificação do cursor e, por fim, a conferência do cursor; o limite
    /// é conferido antes de gastar AES na decodificação.
    /// </summary>
    public async Task<PageRequest?> ReadOrRejectAsync()
    {
        IQueryCollection query = _context.HttpContext.Request.Query;
        if (!TryReadLimit(query[LimitParam], out int? requestedLimit))
        {
            RejectLimitOutOfRange();
            return null;
        }

        if (!TryReadDirection(query[DirectionParam], out PaginationDirection direction))
        {
            RejectUnknownDirection();
            return null;
        }

        return await ReadPageAsync(query[CursorParam], direction, requestedLimit).ConfigureAwait(false);
    }

    private async Task<PageRequest?> ReadPageAsync(string? rawCursor, PaginationDirection direction, int? requestedLimit) =>
        string.IsNullOrWhiteSpace(rawCursor)
            ? FirstPage(requestedLimit)
            : await ReadPageFromCursorAsync(rawCursor, direction, requestedLimit).ConfigureAwait(false);

    /// <summary>Sem ponto de continuação não há o que navegar para trás: a primeira página é sempre para a frente.</summary>
    private PageRequest FirstPage(int? requestedLimit) =>
        new(null, requestedLimit ?? _options.LimitDefault, PaginationDirection.Next, null);

    /// <summary>O limite da consulta vence o do cursor; o do cursor vence o padrão.</summary>
    private async Task<PageRequest?> ReadPageFromCursorAsync(string rawCursor, PaginationDirection direction, int? requestedLimit)
    {
        PageRequest? cursorPage = await ReadCursorAsync(rawCursor, direction).ConfigureAwait(false);
        return cursorPage is null || requestedLimit is not { } request ? cursorPage : cursorPage with { Limit = request };
    }

    private bool TryReadLimit(StringValues raw, out int? limit)
    {
        limit = null;
        string? first = raw.Count == 0 ? null : raw[0];
        if (string.IsNullOrWhiteSpace(first))
        {
            return true;
        }

        bool parsed = int.TryParse(first, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value);
        limit = parsed ? value : int.MinValue;
        return limit >= _options.LimitMin && limit <= _options.LimitMax;
    }

    private static bool TryReadDirection(StringValues raw, out PaginationDirection direction)
    {
        string? first = raw.Count == 0 ? null : raw[0];
        bool isPrevious = string.Equals(first, "prev", StringComparison.OrdinalIgnoreCase);
        direction = isPrevious ? PaginationDirection.Prev : PaginationDirection.Next;

        return isPrevious
            || string.IsNullOrWhiteSpace(first)
            || string.Equals(first, "next", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<PageRequest?> ReadCursorAsync(string rawCursor, PaginationDirection direction)
    {
        CursorDecodeResult decoded = await _encoder
            .TryDecodeAsync(rawCursor, _context.HttpContext.RequestAborted)
            .ConfigureAwait(false);

        if (decoded.Status == CursorDecodeStatus.Success)
        {
            return Accept(decoded.Payload!, direction);
        }

        RejectDecodeStatus(decoded.Status);
        return null;
    }

    private PageRequest? Accept(CursorPayload payload, PaginationDirection direction)
    {
        string resourceTag = _attribute.ResourceTagFor(_context.ActionContext.RouteData);
        if (!Guid.TryParse(payload.After, out Guid pontoDeContinuacao) || !CursorMatches(payload, resourceTag, direction))
        {
            RejectAsInvalid();
            return null;
        }

        // O limite do cursor é a memória do tamanho da página: ajustado se a configuração apertou o intervalo.
        int limit = Math.Clamp(payload.Limit, _options.LimitMin, _options.LimitMax);
        return new PageRequest(pontoDeContinuacao, limit, direction, payload.SortKey);
    }

    /// <summary>
    /// A ordem das conferências é a da recusa: etiqueta da coleção (ADR-0026), direção emitida (ADR-0089),
    /// vínculo à conta e chave de continuação (ADR-0094). A conta só é resolvida depois de etiqueta e direção certas.
    /// Direção diferente da emitida é tratada como adulteração; um cursor só com o identificador não pode cair em
    /// silêncio para a primeira página, porque toda continuação exige o ponto completo.
    /// </summary>
    private bool CursorMatches(CursorPayload payload, string resourceTag, PaginationDirection direction) =>
        string.Equals(payload.ResourceTag, resourceTag, StringComparison.Ordinal)
        && payload.Direction == direction
        && AccountMatches(payload)
        && !string.IsNullOrWhiteSpace(payload.SortKey);

    /// <summary>
    /// Vínculo à conta (ADR-0026): conta autenticada com emissor e identificador; cursor com resumo da conta; e resumos
    /// iguais. As três falhas dão a mesma recusa, para não revelar ao atacante qual delas ocorreu.
    /// </summary>
    private bool AccountMatches(CursorPayload payload)
    {
        if (!_attribute.RequireUserBinding)
        {
            return true;
        }

        IUserContext user = _context.HttpContext.RequestServices.GetRequiredService<IUserContext>();
        string? summary = ContaDeQuemConsulta.Resumir(user);
        return summary is not null
            && string.Equals(payload.ResumoDaConta, summary, StringComparison.Ordinal);
    }

    private void RejectLimitOutOfRange() => Reject(
        CursorBindingErrorCodes.LimitInvalido,
        $"O parâmetro 'limit' deve estar entre {_options.LimitMin} e {_options.LimitMax}.");

    private void RejectUnknownDirection() => Reject(
        CursorBindingErrorCodes.DirecaoInvalida,
        "O parâmetro 'direction' deve ser 'next' ou 'prev'.");

    private void RejectDecodeStatus(CursorDecodeStatus status)
    {
        if (status == CursorDecodeStatus.Expired)
        {
            RejectAsExpired();
        }
        else
        {
            RejectAsInvalid();
        }
    }

    private void RejectAsInvalid() => Reject(
        CursorBindingErrorCodes.Invalido,
        "O cursor informado é inválido.");

    private void RejectAsExpired() => Reject(
        CursorBindingErrorCodes.Expirado,
        "O cursor informado expirou.");

    private void Reject(string code, string detail)
    {
        _context.HttpContext.Items[CursorBindingErrorCodes.HttpContextItemKey] = code;
        _context.ModelState.AddModelError(_context.ModelName, detail);
        _context.Result = ModelBindingResult.Failed();
    }
}
