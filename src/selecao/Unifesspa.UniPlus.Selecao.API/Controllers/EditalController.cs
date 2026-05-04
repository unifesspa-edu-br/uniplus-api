namespace Unifesspa.UniPlus.Selecao.API.Controllers;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.Editais;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.Editais;

[ApiController]
[Route("api/editais")]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public; sem isso o MVC ignora a classe e nenhum endpoint é registrado.")]
public sealed class EditalController : ControllerBase
{
    private const string ResourceTag = "editais";
    private const int LimitPadrao = 20;
    private const int LimitMinimo = 1;
    private const int LimitMaximo = 100;
    private static readonly TimeSpan CursorTtl = TimeSpan.FromMinutes(15);

    private readonly ICommandBus _commandBus;
    private readonly IQueryBus _queryBus;
    private readonly IDomainErrorMapper _mapper;
    private readonly CursorEncoder _cursorEncoder;
    private readonly TimeProvider _timeProvider;

    public EditalController(
        ICommandBus commandBus,
        IQueryBus queryBus,
        IDomainErrorMapper mapper,
        CursorEncoder cursorEncoder,
        TimeProvider timeProvider)
    {
        _commandBus = commandBus;
        _queryBus = queryBus;
        _mapper = mapper;
        _cursorEncoder = cursorEncoder;
        _timeProvider = timeProvider;
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
        int effectiveLimit = limit ?? LimitPadrao;
        if (effectiveLimit < LimitMinimo || effectiveLimit > LimitMaximo)
        {
            DomainError limitErro = new(
                "Cursor.LimitInvalido",
                $"O parâmetro 'limit' deve estar entre {LimitMinimo} e {LimitMaximo}.");
            return Result.Failure(limitErro).ToActionResult(_mapper);
        }

        Guid? afterId = null;
        if (!string.IsNullOrWhiteSpace(cursor))
        {
            CursorDecodeResult decoded = await _cursorEncoder.TryDecodeAsync(cursor, cancellationToken);
            switch (decoded.Status)
            {
                case CursorDecodeStatus.Invalid:
                    return Result.Failure(new DomainError("Cursor.Invalido", "O cursor informado é inválido.")).ToActionResult(_mapper);
                case CursorDecodeStatus.Expired:
                    return Result.Failure(new DomainError("Cursor.Expirado", "O cursor informado expirou.")).ToActionResult(_mapper);
                case CursorDecodeStatus.Success:
                    if (!Guid.TryParse(decoded.Payload!.After, out Guid parsedAfter)
                        || !string.Equals(decoded.Payload.ResourceTag, ResourceTag, StringComparison.Ordinal))
                    {
                        return Result.Failure(new DomainError("Cursor.Invalido", "O cursor informado é inválido.")).ToActionResult(_mapper);
                    }
                    afterId = parsedAfter;
                    effectiveLimit = decoded.Payload.Limit;
                    break;
                default:
                    return Result.Failure(new DomainError("Cursor.Invalido", "O cursor informado é inválido.")).ToActionResult(_mapper);
            }
        }

        ListarEditaisResult page = await _queryBus.Send(
            new ListarEditaisQuery(afterId, effectiveLimit), cancellationToken);

        string? nextCursor = null;
        if (page.ProximoAfterId is { } proximo)
        {
            CursorPayload payload = new(
                After: proximo.ToString(),
                Limit: effectiveLimit,
                ResourceTag: ResourceTag,
                ExpiresAt: _timeProvider.GetUtcNow().Add(CursorTtl));
            nextCursor = await _cursorEncoder.EncodeAsync(payload, cancellationToken);
        }

        PageLinks links = new(
            Self: BuildLink(cursor, effectiveLimit),
            Next: nextCursor is null ? null : BuildLink(nextCursor, effectiveLimit),
            Prev: null);

        Response.Headers["Link"] = LinkHeaderBuilder.Build(links);
        Response.Headers["X-Page-Size"] = page.Items.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);

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

    private string BuildLink(string? cursor, int limit)
    {
        HttpRequest req = Request;
        string baseUrl = $"{req.Scheme}://{req.Host}{req.Path}";
        List<string> parts = [];
        if (!string.IsNullOrEmpty(cursor))
        {
            parts.Add($"cursor={Uri.EscapeDataString(cursor)}");
        }
        parts.Add($"limit={limit.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        return parts.Count == 0 ? baseUrl : $"{baseUrl}?{string.Join('&', parts)}";
    }
}
