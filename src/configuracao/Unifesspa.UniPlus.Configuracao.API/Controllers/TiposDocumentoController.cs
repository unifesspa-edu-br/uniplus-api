namespace Unifesspa.UniPlus.Configuracao.API.Controllers;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.ProtecaoDoCatalogo;
using Unifesspa.UniPlus.Configuracao.Application.Commands.TiposDocumento;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Permissoes;
using Unifesspa.UniPlus.Configuracao.Application.Queries.ProtecaoDoCatalogo;
using Unifesspa.UniPlus.Configuracao.Application.Queries.TiposDocumento;
using Unifesspa.UniPlus.Infrastructure.Core.Authorization;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Infrastructure.Core.Hateoas;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Endpoints públicos de leitura (<c>GET /api/configuracao/tipos-documento</c>,
/// <c>GET /api/configuracao/tipos-documento/{id}</c>) e endpoints admin
/// (<c>POST/PUT/DELETE /api/configuracao/admin/tipos-documento</c>) restritos à
/// permissão de manutenção (UNI-REQ-0013).
/// </summary>
[ApiController]
[Authorize]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public.")]
public sealed class TiposDocumentoController : ControllerBase
{
    private const string ResourceTag = "tipos-documento";

    private readonly ICommandBus _commandBus;
    private readonly IQueryBus _queryBus;
    private readonly IDomainErrorMapper _mapper;
    private readonly IResourceLinksBuilder<TipoDocumentoDto> _linksBuilder;
    private readonly ITitularesDaPermissao _titulares;

    public TiposDocumentoController(
        ICommandBus commandBus,
        IQueryBus queryBus,
        IDomainErrorMapper mapper,
        IResourceLinksBuilder<TipoDocumentoDto> linksBuilder,
        ITitularesDaPermissao titulares)
    {
        _titulares = titulares;
        _commandBus = commandBus;
        _queryBus = queryBus;
        _mapper = mapper;
        _linksBuilder = linksBuilder;
    }

    /// <summary>
    /// Lista os tipos de documento ativos, paginados por cursor opaco bidirecional
    /// (ADR-0026 + ADR-0089). Navegação via header <c>Link</c>; cada item carrega
    /// seu <c>_links.self</c> (ADR-0029).
    /// </summary>
    [HttpGet("tipos-documento")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "tipo-documento", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<TipoDocumentoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Listar(
        [FromCursor(ResourceTag)] PageRequest page,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);

        ListarTiposDocumentoResult resultado = await _queryBus
            .Send(new ListarTiposDocumentoQuery(page.AfterId, page.Limit, page.Direction), cancellationToken)
            .ConfigureAwait(false);

        TipoDocumentoDto[] comLinks =
            [.. resultado.Items.Select(t => t with { Links = _linksBuilder.Build(t) })];

        return await this.OkPaginatedAsync(
            comLinks, resultado.AnteriorAfterId, resultado.ProximoAfterId, page, ResourceTag,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Obtém um tipo de documento pelo Id. Retorna 404 quando inexistente.</summary>
    [HttpGet("tipos-documento/{id:guid}")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "tipo-documento", Versions = [1])]
    [ProducesResponseType(typeof(TipoDocumentoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken cancellationToken)
    {
        TipoDocumentoDto? tipo = await _queryBus
            .Send(new ObterTipoDocumentoPorIdQuery(id), cancellationToken)
            .ConfigureAwait(false);

        if (tipo is null)
        {
            return NotFound();
        }

        TipoDocumentoDto comLinks = tipo with { Links = _linksBuilder.Build(tipo) };
        return Ok(comLinks);
    }

    /// <summary>Cria um novo tipo de documento. Exige a permissão de manutenção. Idempotency-Key obrigatório (ADR-0027).</summary>
    [HttpPost("admin/tipos-documento")]
    [ExigePermissao<PermissaoConfiguracao>(PermissaoConfiguracao.TiposDocumentoManter, ResourceTipo = ResourceTiposConfiguracao.TipoDocumento)]
    [RequiresIdempotencyKey]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Criar(
        [FromBody] CriarTipoDocumentoCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        Result<Guid> resultado = await _commandBus.Send(command, cancellationToken).ConfigureAwait(false);

        return resultado.IsSuccess
            ? CreatedAtAction(nameof(ObterPorId), new { id = resultado.Value }, resultado.Value)
            : resultado.ToActionResult(_mapper);
    }

    /// <summary>Atualiza um tipo de documento existente. Exige a permissão de manutenção.</summary>
    [HttpPut("admin/tipos-documento/{id:guid}")]
    [ExigePermissao<PermissaoConfiguracao>(PermissaoConfiguracao.TiposDocumentoManter, ResourceTipo = ResourceTiposConfiguracao.TipoDocumento)]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Atualizar(
        Guid id,
        [FromBody] AtualizarTipoDocumentoCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (id != command.Id)
        {
            return IdDivergente();
        }

        Result resultado = await _commandBus.Send(command, cancellationToken).ConfigureAwait(false);

        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Aceita a classificação que a proteção de dados recomendou ao negar o tipo: ele fica aprovado com ela,
    /// sem nova solicitação, e a proteção de dados é avisada. Exige a permissão de manutenção.
    /// </summary>
    [HttpPost("admin/tipos-documento/{id:guid}/aceite-da-recomendacao")]
    [ExigePermissao<PermissaoConfiguracao>(PermissaoConfiguracao.TiposDocumentoManter, ResourceTipo = ResourceTiposConfiguracao.TipoDocumento)]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AceitarRecomendacao(Guid id, CancellationToken cancellationToken)
    {
        // Os destinatários do aviso vêm do módulo Autorização e são lidos antes do handler, que fica num único DbContext.
        IReadOnlyList<string> destinatarios = await _titulares
            .ListarEmailsAsync(PermissaoConfiguracao.ProtecaoDoCatalogoAprovar.Codigo, cancellationToken)
            .ConfigureAwait(false);

        Result resultado = await _commandBus
            .Send(new AceitarRecomendacaoDoTipoDocumentoCommand(id, destinatarios), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Todos os atos registrados da proteção de dados do tipo — solicitação, decisão e aceite —, em ordem cronológica, com o nome de quem agiu. O tipo de documento não tem permissão de consulta administrativa (a leitura é pública), então exige a permissão de manutenção ou a de consultar a proteção de dados do catálogo. A leitura é registrada antes da resposta; sem o registro, é recusada com 503.
    /// </summary>
    [HttpGet("admin/tipos-documento/{id:guid}/protecao/atos")]
    [ExigePermissao<PermissaoConfiguracao>(
        PermissaoConfiguracao.TiposDocumentoManter,
        ResourceTipo = ResourceTiposConfiguracao.TipoDocumento,
        OuPermissao = PermissaoConfiguracao.ProtecaoDoCatalogoConsultar)]
    [VendorMediaType(Resource = "protecao-do-catalogo-ato", Versions = [1])]
    [ProducesResponseType(typeof(IReadOnlyList<AtoDaProtecaoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ListarAtosDaProtecao(Guid id, CancellationToken cancellationToken)
    {
        IReadOnlyList<AtoDaProtecaoDto>? atos = await _queryBus
            .Send(new ListarAtosDoItemDaProtecaoQuery(CadastroDaProtecaoDoCatalogo.TipoDocumento, id), cancellationToken)
            .ConfigureAwait(false);

        return atos is null ? NotFound() : Ok(atos);
    }

    /// <summary>Remove (soft-delete) um tipo de documento. Exige a permissão de manutenção.</summary>
    [HttpDelete("admin/tipos-documento/{id:guid}")]
    [ExigePermissao<PermissaoConfiguracao>(PermissaoConfiguracao.TiposDocumentoManter, ResourceTipo = ResourceTiposConfiguracao.TipoDocumento)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Remover(Guid id, CancellationToken cancellationToken)
    {
        Result resultado = await _commandBus
            .Send(new RemoverTipoDocumentoCommand(id), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }

    private BadRequestObjectResult IdDivergente() =>
        BadRequest(new ProblemDetails
        {
            Title = "Id divergente",
            Detail = "O Id na URL não corresponde ao Id no corpo da requisição.",
            Status = StatusCodes.Status400BadRequest,
        });
}
