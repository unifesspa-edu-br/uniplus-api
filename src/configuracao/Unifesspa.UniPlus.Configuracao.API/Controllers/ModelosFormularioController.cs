namespace Unifesspa.UniPlus.Configuracao.API.Controllers;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Queries.ModelosFormulario;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// Manutenção dos modelos de formulário pelo papel <c>plataforma-admin</c> (UNI-REQ-0144,
/// ADR-0137): cadastro, edição, desativação e reativação. O processo parte de um modelo e recebe
/// uma cópia; mudar o modelo depois não muda o processo.
/// </summary>
[ApiController]
[Authorize(Roles = "plataforma-admin")]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public.")]
public sealed class ModelosFormularioController : ControllerBase
{
    private const string ResourceTag = "admin-modelos-formulario";
    private readonly ICommandBus _commandBus;
    private readonly IQueryBus _queryBus;
    private readonly IDomainErrorMapper _mapper;

    public ModelosFormularioController(ICommandBus commandBus, IQueryBus queryBus, IDomainErrorMapper mapper)
    {
        _commandBus = commandBus;
        _queryBus = queryBus;
        _mapper = mapper;
    }

    /// <summary>
    /// Lista paginada, com filtros opcionais: o tipo de processo traz os modelos que servem a ele,
    /// inclusive os que servem a todos; a finalidade é o token canônico.
    /// </summary>
    [HttpGet("admin/modelos-formulario")]
    [VendorMediaType(Resource = "modelo-formulario", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<ModeloFormularioView>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Listar(
        [FromCursor(ResourceTag)] PageRequest page,
        [FromQuery] string? tipoProcesso = null,
        [FromQuery] string? finalidade = null,
        [FromQuery] bool? ativo = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);
        ListarModelosFormularioResult resultado = await _queryBus.Send(
            new ListarModelosFormularioQuery(page.AfterId, page.Limit, page.Direction, tipoProcesso, finalidade, ativo), cancellationToken)
            .ConfigureAwait(false);
        return await this.OkPaginatedAsync(
            resultado.Items, resultado.AnteriorAfterId, resultado.ProximoAfterId, page, ResourceTag, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>O modelo, ativo ou desativado, com o conteúdo no formato da escrita.</summary>
    [HttpGet("admin/modelos-formulario/{id:guid}")]
    [VendorMediaType(Resource = "modelo-formulario", Versions = [1])]
    [ProducesResponseType(typeof(ModeloFormularioView), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> Obter(Guid id, CancellationToken cancellationToken)
    {
        ModeloFormularioView? modelo = await _queryBus.Send(new ObterModeloFormularioQuery(id), cancellationToken).ConfigureAwait(false);
        return modelo is null ? NotFound() : Ok(modelo);
    }

    /// <summary>
    /// O modelo no formato do formulário renderizável — o mesmo do certame divulgado e do rascunho do
    /// processo —, para a simulação interpretar o modelo antes de aplicá-lo. Não grava nada.
    /// </summary>
    [HttpGet("admin/modelos-formulario/{id:guid}/renderizavel")]
    [VendorMediaType(Resource = "formulario", Versions = [2])]
    [ProducesResponseType(typeof(FormularioRenderizavel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ObterRenderizavel(Guid id, CancellationToken cancellationToken)
    {
        FormularioRenderizavel? formulario = await _queryBus
            .Send(new ObterFormularioRenderizavelDoModeloQuery(id), cancellationToken).ConfigureAwait(false);
        return formulario is null ? NotFound() : Ok(formulario);
    }

    /// <summary>
    /// Pré-visualiza o modelo com respostas simuladas: o que cada item, cada termo e cada grupo
    /// repetível faria diante delas, pelo mesmo avaliador da inscrição. Não grava nada.
    /// </summary>
    [HttpPost("admin/modelos-formulario/{id:guid}/pre-visualizacao")]
    [VendorMediaType(Resource = "pre-visualizacao-modelo-formulario", Versions = [1])]
    [ProducesResponseType(typeof(PreVisualizacaoDoModeloDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> PreVisualizar(Guid id, [FromBody] PreVisualizacaoDoModeloInput simulacao, CancellationToken cancellationToken)
    {
        Result<PreVisualizacaoDoModeloDto?> resultado = await _queryBus
            .Send(new PreVisualizarModeloFormularioQuery(id, simulacao), cancellationToken).ConfigureAwait(false);
        if (resultado.IsFailure)
        {
            return resultado.ToActionResult(_mapper);
        }

        return resultado.Value is { } preVisualizacao ? Ok(preVisualizacao) : NotFound();
    }

    /// <summary>
    /// Cadastra o modelo desativado; ele entra na escolha de processos novos pela ativação. O código
    /// e a finalidade são imutáveis; o conteúdo é conferido contra o catálogo de fatos, de termos e de
    /// tipos de processo.
    /// </summary>
    [HttpPost("admin/modelos-formulario")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Criar([FromBody] CriarModeloFormularioCommand command, CancellationToken cancellationToken)
    {
        Result<Guid> resultado = await _commandBus.Send(command, cancellationToken).ConfigureAwait(false);
        return resultado.IsSuccess
            ? CreatedAtAction(nameof(Obter), new { id = resultado.Value }, resultado.Value)
            : resultado.ToActionResult(_mapper);
    }

    /// <summary>Substitui o nome, a descrição, o tipo de processo e o conteúdo do modelo.</summary>
    [HttpPut("admin/modelos-formulario/{id:guid}")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> Atualizar(Guid id, [FromBody] EdicaoDoModeloInput request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Enviar(
            new AtualizarModeloFormularioCommand(id, request.Nome, request.Descricao, request.TipoProcessoCodigo, request.Conteudo),
            cancellationToken);
    }

    /// <summary>Reativa o modelo, que volta à escolha dos processos novos.</summary>
    [HttpPost("admin/modelos-formulario/{id:guid}/ativacao")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> Reativar(Guid id, CancellationToken cancellationToken) =>
        Enviar(new AtivarModeloFormularioCommand(id), cancellationToken);

    /// <summary>Desativa o modelo; os processos que já o copiaram não mudam. O modelo não é apagado.</summary>
    [HttpDelete("admin/modelos-formulario/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> Desativar(Guid id, CancellationToken cancellationToken) =>
        Enviar(new DesativarModeloFormularioCommand(id), cancellationToken);

    private async Task<IActionResult> Enviar(ICommand<Result> command, CancellationToken cancellationToken)
    {
        Result resultado = await _commandBus.Send(command, cancellationToken).ConfigureAwait(false);
        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }
}
