namespace Unifesspa.UniPlus.Selecao.API.Controllers;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Results;
using Configuration;
using Application.Commands.Editais;
using Application.DTOs;
using Application.Queries.Editais;

[ApiController]
[Route("api/editais")]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public; sem isso o MVC ignora a classe e nenhum endpoint é registrado.")]
public sealed class EditalController : ControllerBase
{
    private const string ResourceTag = "editais";

    private readonly ICommandBus _commandBus;
    private readonly IQueryBus _queryBus;
    private readonly IDomainErrorMapper _mapper;
    private readonly CursorEncoder _cursorEncoder;
    private readonly TimeProvider _timeProvider;
    private readonly EditalPaginationOptions _paginationOptions;

    public EditalController(
        ICommandBus commandBus,
        IQueryBus queryBus,
        IDomainErrorMapper mapper,
        CursorEncoder cursorEncoder,
        TimeProvider timeProvider,
        IOptions<EditalPaginationOptions> paginationOptions)
    {
        ArgumentNullException.ThrowIfNull(paginationOptions);

        _commandBus = commandBus;
        _queryBus = queryBus;
        _mapper = mapper;
        _cursorEncoder = cursorEncoder;
        _timeProvider = timeProvider;
        _paginationOptions = paginationOptions.Value;
    }

    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Criar([FromBody] CriarEditalCommand command, CancellationToken cancellationToken)
    {
        Result<Guid> resultado = await _commandBus.Send(command, cancellationToken);
        if (resultado.IsSuccess)
            return CreatedAtAction(nameof(ObterPorId), new { id = resultado.Value }, resultado.Value);
        return resultado.ToActionResult(_mapper);
    }

    [HttpGet]
    [VendorMediaType(Resource = "edital", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<EditalDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Listar(
        [FromQuery] string? cursor,
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        // Validação de limit do query string acontece antes do decode — falha
        // rápida sem gastar AES-GCM em request mal-formado.
        if (limit is { } requestedLimit && !IsLimitInRange(requestedLimit))
            return LimitInvalido();

        Guid? afterId = null;
        int? cursorLimit = null;

        if (!string.IsNullOrWhiteSpace(cursor))
        {
            CursorDecodeResult decoded = await _cursorEncoder.TryDecodeAsync(cursor, cancellationToken);
            switch (decoded.Status)
            {
                case CursorDecodeStatus.Invalid:
                    return CursorInvalido();
                case CursorDecodeStatus.Expired:
                    return Result.Failure(new DomainError("Cursor.Expirado", "O cursor informado expirou.")).ToActionResult(_mapper);
                case CursorDecodeStatus.Success:
                    if (!Guid.TryParse(decoded.Payload!.After, out Guid parsedAfter)
                        || !string.Equals(decoded.Payload.ResourceTag, ResourceTag, StringComparison.Ordinal))
                    {
                        return CursorInvalido();
                    }
                    afterId = parsedAfter;
                    // Limit do cursor é "memória" do tamanho de janela usado pelo
                    // cliente; clampado contra o range corrente caso a config
                    // tenha sido apertada após a emissão.
                    cursorLimit = Math.Clamp(decoded.Payload.Limit, _paginationOptions.LimitMin, _paginationOptions.LimitMax);
                    break;
                default:
                    return CursorInvalido();
            }
        }

        // Precedência: query string vence sobre cursor; cursor vence sobre default.
        // Cliente que queira mudar tamanho de janela mid-navigation passa ?limit=N
        // junto do cursor — o keyset (afterId) é o que mantém estabilidade, não o
        // limit.
        int effectiveLimit = limit ?? cursorLimit ?? _paginationOptions.LimitDefault;

        ListarEditaisResult page = await _queryBus.Send(
            new ListarEditaisQuery(afterId, effectiveLimit), cancellationToken);

        string? nextCursor = null;
        if (page.ProximoAfterId is { } proximo)
        {
            // NOTA: para endpoints user-scoped futuros (/inscricoes, /recursos),
            // o payload do cursor precisa carregar UserId além de After/ResourceTag
            // para impedir que cursor de Alice seja navegável por Bob (gap LGPD
            // que não existe aqui porque editais são públicos).
            CursorPayload payload = new(
                After: proximo.ToString(),
                Limit: effectiveLimit,
                ResourceTag: ResourceTag,
                ExpiresAt: _timeProvider.GetUtcNow().Add(_paginationOptions.CursorTtl));
            nextCursor = await _cursorEncoder.EncodeAsync(payload, cancellationToken);
        }

        PageLinks links = new(
            Self: BuildLink(cursor, limit),
            Next: nextCursor is null ? null : BuildLink(nextCursor, effectiveLimit),
            Prev: null);

        Response.Headers["Link"] = LinkHeaderBuilder.Build(links);
        Response.Headers["X-Page-Size"] = page.Items.Count.ToString(CultureInfo.InvariantCulture);

        return Ok(page.Items);
    }

    [HttpGet("{id:guid}")]
    [VendorMediaType(Resource = "edital", Versions = [1])]
    [ProducesResponseType(typeof(EditalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken cancellationToken)
    {
        EditalDto? edital = await _queryBus.Send(new ObterEditalQuery(id), cancellationToken);
        return edital is not null ? Ok(edital) : NotFound();
    }

    // Despacha PublicarEditalCommand pelo ICommandBus (Wolverine). O handler
    // convention-based atualiza o agregado e drena EditalPublicadoEvent por
    // cascading messages — atomicidade write+evento garantida pela
    // IEnvelopeTransaction da configuração produtiva (ADR-0005).
    [HttpPost("{id:guid}/publicar")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Publicar(Guid id, CancellationToken cancellationToken)
    {
        Result resultado = await _commandBus.Send(new PublicarEditalCommand(id), cancellationToken);
        if (resultado.IsSuccess)
            return NoContent();
        return resultado.ToActionResult(_mapper);
    }

    private bool IsLimitInRange(int limit) =>
        limit >= _paginationOptions.LimitMin && limit <= _paginationOptions.LimitMax;

    private IActionResult LimitInvalido() =>
        Result.Failure(new DomainError(
            "Cursor.LimitInvalido",
            $"O parâmetro 'limit' deve estar entre {_paginationOptions.LimitMin} e {_paginationOptions.LimitMax}."))
        .ToActionResult(_mapper);

    private IActionResult CursorInvalido() =>
        Result.Failure(new DomainError("Cursor.Invalido", "O cursor informado é inválido."))
        .ToActionResult(_mapper);

    private string BuildLink(string? cursor, int? limit)
    {
        HttpRequest req = Request;
        string baseUrl = $"{req.Scheme}://{req.Host}{req.Path}";
        List<string> parts = [];
        if (!string.IsNullOrEmpty(cursor))
            parts.Add($"cursor={Uri.EscapeDataString(cursor)}");
        if (limit is { } l)
            parts.Add($"limit={l.ToString(CultureInfo.InvariantCulture)}");
        return parts.Count == 0 ? baseUrl : $"{baseUrl}?{string.Join('&', parts)}";
    }
}
