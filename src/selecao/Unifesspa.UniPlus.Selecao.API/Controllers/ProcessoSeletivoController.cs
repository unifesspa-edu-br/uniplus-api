namespace Unifesspa.UniPlus.Selecao.API.Controllers;

using System.Diagnostics.CodeAnalysis;

using Application.Commands.ProcessosSeletivos;
using Application.DTOs;
using Application.Queries.ProcessosSeletivos;

using Http;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Infrastructure.Core.Hateoas;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.API.Authorization;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Cria, lista e lê o processo seletivo. O administrador cria o processo em rascunho e monta a
/// configuração pelas demais rotas de <c>processos-seletivos</c>, que ficam em controllers próprios
/// da mesma tag.
/// </summary>
/// <remarks>
/// <para>
/// Cada ação de configuração exige a permissão da sua operação na unidade dona do processo
/// (<c>ExigePermissaoNoProcesso</c>), decidida pela concessão e não pelo papel no token.
/// <c>[Authorize]</c> mantém anônimo quem não se autenticou, porque não há fallback policy.
/// </para>
/// <para>
/// A leitura do processo é aberta depois da publicação e restrita a quem configura enquanto é
/// rascunho (<c>ExigeLeituraDoProcesso</c>, anônima). A listagem não tem processo para decidir:
/// devolve os processos publicados e os rascunhos das unidades em que a pessoa configura.
/// </para>
/// </remarks>
[ApiController]
[Route("processos-seletivos")]
[Tags("ProcessoSeletivo")]
[Authorize]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public; sem isso o MVC ignora a classe e nenhum endpoint é registrado.")]
public sealed class ProcessoSeletivoController : ControllerBase
{
    private const string ResourceTag = "processos-seletivos";

    private readonly ICommandBus _commandBus;
    private readonly IQueryBus _queryBus;
    private readonly IDomainErrorMapper _mapper;
    private readonly IResourceLinksBuilder<ProcessoSeletivoDto> _linksBuilder;

    public ProcessoSeletivoController(
        ICommandBus commandBus,
        IQueryBus queryBus,
        IDomainErrorMapper mapper,
        IResourceLinksBuilder<ProcessoSeletivoDto> linksBuilder)
    {
        _commandBus = commandBus;
        _queryBus = queryBus;
        _mapper = mapper;
        _linksBuilder = linksBuilder;
    }

    [HttpPost]
    [RequiresIdempotencyKey]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict,
        Description = "O identificador legível informado já é usado por outro processo seletivo (uniplus.selecao.processo_seletivo.identificador_legivel_em_uso). Também responde 409 quando outra requisição com a mesma Idempotency-Key ainda está em processamento.")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Criar([FromBody] CriarProcessoSeletivoCommand command, CancellationToken cancellationToken)
    {
        Result<Guid> resultado = await _commandBus.Send(command, cancellationToken);
        if (resultado.IsSuccess)
            return CreatedAtAction(nameof(ObterPorId), new { id = resultado.Value }, resultado.Value);
        return resultado.ToActionResult(_mapper);
    }

    [AllowAnonymous]
    [HttpGet]
    [VendorMediaType(Resource = "processo-seletivo", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<ProcessoSeletivoResumoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Listar(
        [FromCursor(ResourceTag)] PageRequest page,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);

        ListarProcessosSeletivosResult resultado = await _queryBus.Send(
            new ListarProcessosSeletivosQuery(page.AfterId, page.Limit, page.Direction), cancellationToken);

        return await this.OkPaginatedAsync(
            resultado.Items, resultado.AnteriorAfterId, resultado.ProximoAfterId, page, ResourceTag,
            cancellationToken: cancellationToken);
    }

    [AllowAnonymous]
    [ExigeLeituraDoProcesso]
    [HttpGet("{id:guid}")]
    [VendorMediaType(Resource = "processo-seletivo", Versions = [1])]
    [ProducesResponseType(typeof(ProcessoSeletivoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken cancellationToken)
    {
        ProcessoSeletivoDto? processo = await _queryBus.Send(new LerProcessoSeletivoQuery(id), cancellationToken);
        if (processo is null)
        {
            return NotFound();
        }

        // HATEOAS Level 1 — ver ADR-0029 para a justificativa completa.
        ProcessoSeletivoDto processoComLinks = processo with { Links = _linksBuilder.Build(processo) };
        return Ok(processoComLinks);
    }
}
