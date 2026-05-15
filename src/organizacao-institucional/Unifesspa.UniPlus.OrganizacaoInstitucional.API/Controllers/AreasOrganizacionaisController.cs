namespace Unifesspa.UniPlus.OrganizacaoInstitucional.API.Controllers;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Infrastructure.Core.Hateoas;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.AreasOrganizacionais;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.DTOs;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Queries.AreasOrganizacionais;

/// <summary>
/// Endpoints públicos de leitura (<c>GET /api/areas-organizacionais</c>,
/// <c>GET /api/areas-organizacionais/{codigo}</c>) e endpoint admin
/// (<c>POST /api/admin/areas-organizacionais</c>) restrito a
/// <c>plataforma-admin</c>.
/// </summary>
/// <remarks>
/// As rotas público/admin estão em caminhos distintos
/// (<c>api/areas-organizacionais</c> vs <c>api/admin/areas-organizacionais</c>) —
/// o controller declara <c>[Route("api")]</c> e cada action especifica seu
/// caminho. Mantém um único controller para o recurso, evitando split entre
/// duas classes só por diferença de path prefix.
/// </remarks>
[ApiController]
[Route("api")]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public; sem isso o MVC ignora a classe.")]
public sealed class AreasOrganizacionaisController : ControllerBase
{
    private readonly ICommandBus _commandBus;
    private readonly IQueryBus _queryBus;
    private readonly IDomainErrorMapper _mapper;
    private readonly IResourceLinksBuilder<AreaOrganizacionalDto> _linksBuilder;

    public AreasOrganizacionaisController(
        ICommandBus commandBus,
        IQueryBus queryBus,
        IDomainErrorMapper mapper,
        IResourceLinksBuilder<AreaOrganizacionalDto> linksBuilder)
    {
        _commandBus = commandBus;
        _queryBus = queryBus;
        _mapper = mapper;
        _linksBuilder = linksBuilder;
    }

    /// <summary>
    /// Lista todas as áreas organizacionais ativas. Sem cursor — catálogo é
    /// bounded reference data (exceção deliberada a ADR-0026).
    /// </summary>
    [HttpGet("areas-organizacionais")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "area-organizacional", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<AreaOrganizacionalDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> Listar(CancellationToken cancellationToken)
    {
        IReadOnlyList<AreaOrganizacionalDto> areas = await _queryBus
            .Send(new ListarAreasOrganizacionaisAtivasQuery(), cancellationToken)
            .ConfigureAwait(false);
        return Ok(areas);
    }

    /// <summary>
    /// Obtém uma área pelo código (uppercase normalizado). Retorna 404 quando
    /// inexistente ou código inválido — uniformidade com GET /resource/{id}.
    /// </summary>
    [HttpGet("areas-organizacionais/{codigo}")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "area-organizacional", Versions = [1])]
    [ProducesResponseType(typeof(AreaOrganizacionalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ObterPorCodigo(string codigo, CancellationToken cancellationToken)
    {
        AreaOrganizacionalDto? area = await _queryBus
            .Send(new ObterAreaOrganizacionalPorCodigoQuery(codigo), cancellationToken)
            .ConfigureAwait(false);

        if (area is null)
        {
            return NotFound();
        }

        // HATEOAS Level 1 (ADR-0029/0049). Action links nunca aqui.
        AreaOrganizacionalDto comLinks = area with { Links = _linksBuilder.Build(area) };
        return Ok(comLinks);
    }

    /// <summary>
    /// Cria uma nova área organizacional. Restrito a <c>plataforma-admin</c>
    /// per ADR-0057 (closed roster). Idempotency-Key obrigatório (ADR-0027).
    /// </summary>
    [HttpPost("admin/areas-organizacionais")]
    [Authorize(Roles = "plataforma-admin")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Criar(
        [FromBody] CriarAreaOrganizacionalCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        Result<Guid> resultado = await _commandBus
            .Send(command, cancellationToken)
            .ConfigureAwait(false);

        if (resultado.IsSuccess)
        {
            return CreatedAtAction(
                nameof(ObterPorCodigo),
                new { codigo = command.Codigo },
                resultado.Value);
        }

        return resultado.ToActionResult(_mapper);
    }
}
