namespace Unifesspa.UniPlus.Geo.API.Controllers;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Geo.Application.Abstractions;
using Unifesspa.UniPlus.Geo.Application.DTOs;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Hateoas;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Gatilho administrativo da atualização periódica do ETL DNE (Story #674), sob o
/// prefixo <c>/api/admin/...</c> e restrito ao role <c>plataforma-admin</c>. Dispara uma
/// carga (<c>202 Accepted</c>, execução em segundo plano) e expõe o acompanhamento. Um
/// job externo (cron de infra) chama o disparo mensalmente — não há scheduler embutido.
/// </summary>
/// <remarks>
/// Não usa <c>Idempotency-Key</c> (ADR-0027): a proteção contra disparos duplicados é o
/// lock de concorrência (índice único parcial → <c>409</c> enquanto há carga em
/// andamento) e a idempotência de fundo vem do upsert por chave natural do ETL.
/// </remarks>
[ApiController]
[Route("api")]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public.")]
public sealed class GeoImportacoesController : ControllerBase
{
    private readonly IGeoImportacaoService _servico;
    private readonly IDomainErrorMapper _mapper;
    private readonly IResourceLinksBuilder<ImportacaoGeoDto> _linksBuilder;
    private readonly IUserContext _userContext;

    public GeoImportacoesController(
        IGeoImportacaoService servico,
        IDomainErrorMapper mapper,
        IResourceLinksBuilder<ImportacaoGeoDto> linksBuilder,
        IUserContext userContext)
    {
        _servico = servico;
        _mapper = mapper;
        _linksBuilder = linksBuilder;
        _userContext = userContext;
    }

    /// <summary>
    /// Dispara a importação de uma versão (AAAAMM) do dataset DNE. Restrito a
    /// <c>plataforma-admin</c>. Retorna <c>202 Accepted</c> com link de acompanhamento;
    /// <c>409</c> se já houver uma carga em andamento.
    /// </summary>
    [HttpPost("admin/geo/importacoes")]
    [Authorize(Roles = "plataforma-admin")]
    [ProducesResponseType(typeof(ImportacaoGeoDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Disparar(
        [FromBody] DispararImportacaoRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        string disparadoPor = _userContext.UserId ?? "desconhecido";

        Result<Guid> resultado = await _servico
            .IniciarAsync(request.Versao, disparadoPor, cancellationToken)
            .ConfigureAwait(false);

        if (resultado.IsFailure)
        {
            return resultado.ToActionResult(_mapper);
        }

        ImportacaoGeoDto? dto = await _servico.ObterAsync(resultado.Value, cancellationToken).ConfigureAwait(false);
        if (dto is null)
        {
            // A execução acabou de ser registrada (mesmo contexto); não recuperá-la é uma
            // inconsistência interna — 500 explícito, não um 202 degradado sem corpo/Location.
            return Problem(
                title: "Falha ao recuperar a execução recém-registrada.",
                statusCode: StatusCodes.Status500InternalServerError);
        }

        ImportacaoGeoDto comLinks = dto with { Links = _linksBuilder.Build(dto) };
        return AcceptedAtAction(nameof(Obter), routeValues: new { id = resultado.Value }, value: comLinks);
    }

    /// <summary>
    /// Obtém o estado e o relatório de uma execução do ETL pelo Id. Restrito a
    /// <c>plataforma-admin</c>.
    /// </summary>
    [HttpGet("admin/geo/importacoes/{id:guid}")]
    [Authorize(Roles = "plataforma-admin")]
    [ProducesResponseType(typeof(ImportacaoGeoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Obter(Guid id, CancellationToken cancellationToken)
    {
        ImportacaoGeoDto? dto = await _servico.ObterAsync(id, cancellationToken).ConfigureAwait(false);
        if (dto is null)
        {
            return NotFound();
        }

        ImportacaoGeoDto comLinks = dto with { Links = _linksBuilder.Build(dto) };
        return Ok(comLinks);
    }
}
