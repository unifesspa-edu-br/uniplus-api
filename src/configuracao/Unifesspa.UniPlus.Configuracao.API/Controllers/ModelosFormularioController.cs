namespace Unifesspa.UniPlus.Configuracao.API.Controllers;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Permissoes;
using Unifesspa.UniPlus.Configuracao.Application.Queries.ModelosFormulario;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Infrastructure.Core.Authorization;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// Manutenção dos modelos de formulário pela permissão de manter (UNI-REQ-0144,
/// ADR-0137): cadastro, edição, desativação e reativação. O processo parte de um modelo e recebe
/// uma cópia; mudar o modelo depois não muda o processo.
/// </summary>
[ApiController]
[Authorize]
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
    /// inclusive os que servem a todos; <c>serveAFaseDeInscricao</c> separa os modelos que servem à fase de
    /// inscrição dos que servem a outra fase.
    /// </summary>
    [HttpGet("admin/modelos-formulario")]
    [EndpointName("ListarModelosDeFormulario")]
    [ExigePermissao<PermissaoConfiguracao>(
        PermissaoConfiguracao.ModelosFormularioConsultar,
        OuEmAlgumaUnidade = PermissaoConfiguracao.ModelosFormularioConsultarNaUnidade,
        ResourceTipo = ResourceTiposConfiguracao.ModeloFormulario)]
    [VendorMediaType(Resource = "modelo-formulario", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<ModeloFormularioDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    public async Task<IActionResult> Listar(
        [FromCursor(ResourceTag)] PageRequest page,
        [FromQuery] string? tipoProcesso = null,
        [FromQuery] bool? serveAFaseDeInscricao = null,
        [FromQuery] bool? ativo = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);
        ListarModelosFormularioResult resultado = await _queryBus.Send(
            new ListarModelosFormularioQuery(page.AfterSortKey, page.AfterId, page.Limit, page.Direction, tipoProcesso, serveAFaseDeInscricao, ativo), cancellationToken)
            .ConfigureAwait(false);

        return await this.OkPaginatedAsync(
            resultado.Items, resultado.Anterior, resultado.Proximo, page, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>O modelo, ativo ou desativado, com o conteúdo no formato da escrita. Quando inexistente, responde 404 com o código de erro de modelo não encontrado.</summary>
    [HttpGet("admin/modelos-formulario/{id:guid}")]
    [EndpointName("ObterModeloDeFormulario")]
    [ExigePermissao<PermissaoConfiguracao>(
        PermissaoConfiguracao.ModelosFormularioConsultar,
        OuEmAlgumaUnidade = PermissaoConfiguracao.ModelosFormularioConsultarNaUnidade,
        ResourceTipo = ResourceTiposConfiguracao.ModeloFormulario)]
    [VendorMediaType(Resource = "modelo-formulario", Versions = [1])]
    [ProducesResponseType(typeof(ModeloFormularioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> Obter(Guid id, CancellationToken cancellationToken)
    {
        Result<ModeloFormularioDto> resultado = await _queryBus
            .Send(new ObterModeloFormularioQuery(id), cancellationToken).ConfigureAwait(false);
        return resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// O modelo no formato do formulário apresentado — o mesmo do certame divulgado e do rascunho do
    /// processo —, para a simulação interpretar o modelo antes de aplicá-lo. Não grava nada.
    /// Quando o modelo não existe, responde 404 com o código de erro de modelo não encontrado.
    /// </summary>
    [HttpGet("admin/modelos-formulario/{id:guid}/apresentacao")]
    [EndpointName("ObterApresentacaoDoModeloDeFormulario")]
    [ExigePermissao<PermissaoConfiguracao>(
        PermissaoConfiguracao.ModelosFormularioConsultar,
        OuEmAlgumaUnidade = PermissaoConfiguracao.ModelosFormularioConsultarNaUnidade,
        ResourceTipo = ResourceTiposConfiguracao.ModeloFormulario)]
    [VendorMediaType(Resource = "formulario", Versions = [2])]
    [ProducesResponseType(typeof(FormularioApresentado), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ObterFormularioApresentado(Guid id, CancellationToken cancellationToken)
    {
        Result<FormularioApresentado> resultado = await _queryBus
            .Send(new ObterFormularioApresentadoDoModeloQuery(id), cancellationToken).ConfigureAwait(false);
        return resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Avalia as regras de um formulário apresentado contra respostas simuladas, sem cadastro nenhum:
    /// é a conferência autoritativa da simulação de um arquivo importado, de um caso do corpus, de um
    /// modelo ou de um processo. Não grava nada nem registra o conteúdo.
    /// </summary>
    [HttpPost("admin/avaliacoes-de-formulario")]
    [EndpointName("AvaliarFormularioSemCadastro")]
    [ExigePermissao<PermissaoConfiguracao>(
        PermissaoConfiguracao.ModelosFormularioConsultar,
        OuEmAlgumaUnidade = PermissaoConfiguracao.ModelosFormularioConsultarNaUnidade,
        ResourceTipo = ResourceTiposConfiguracao.ModeloFormulario)]
    [VendorMediaType(Resource = "avaliacao-de-formulario", Versions = [1])]
    [ProducesResponseType(typeof(AvaliacaoPortavel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AvaliarSemCadastro([FromBody] AvaliacaoSemCadastroInput simulacao, CancellationToken cancellationToken)
    {
        Result<AvaliacaoPortavel> resultado = await _queryBus
            .Send(new AvaliarFormularioSemCadastroQuery(simulacao), cancellationToken).ConfigureAwait(false);
        return resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Pré-visualiza o modelo com respostas simuladas: o que cada item, cada termo e cada grupo
    /// repetível faria diante delas, pelo mesmo avaliador da inscrição. Não grava nada.
    /// Quando o modelo não existe, responde 404 com o código de erro de modelo não encontrado.
    /// </summary>
    [HttpPost("admin/modelos-formulario/{id:guid}/pre-visualizacao")]
    [EndpointName("PreVisualizarModeloDeFormulario")]
    [ExigePermissao<PermissaoConfiguracao>(
        PermissaoConfiguracao.ModelosFormularioConsultar,
        OuEmAlgumaUnidade = PermissaoConfiguracao.ModelosFormularioConsultarNaUnidade,
        ResourceTipo = ResourceTiposConfiguracao.ModeloFormulario)]
    [VendorMediaType(Resource = "pre-visualizacao-modelo-formulario", Versions = [1])]
    [ProducesResponseType(typeof(PreVisualizacaoDoModeloDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> PreVisualizar(Guid id, [FromBody] PreVisualizacaoDoModeloInput simulacao, CancellationToken cancellationToken)
    {
        Result<PreVisualizacaoDoModeloDto> resultado = await _queryBus
            .Send(new PreVisualizarModeloFormularioQuery(id, simulacao), cancellationToken).ConfigureAwait(false);
        return resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Cadastra o modelo desativado; ele entra na escolha de processos novos pela ativação. O código
    /// e a declaração — se serve à fase de inscrição e se é obrigatório — são imutáveis; o conteúdo é conferido contra o catálogo de fatos, de termos e de
    /// tipos de processo.
    /// </summary>
    [HttpPost("admin/modelos-formulario")]
    [EndpointName("CadastrarModeloDeFormulario")]
    [ExigePermissao<PermissaoConfiguracao>(PermissaoConfiguracao.ModelosFormularioManter, ResourceTipo = ResourceTiposConfiguracao.ModeloFormulario)]
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

    /// <summary>
    /// Substitui o descritivo e o conteúdo do modelo. O corpo é o <c>descritivo</c> e o <c>conteudo</c> da leitura
    /// do modelo, e o conteúdo substitui o gravado em bloco; <c>id</c>, <c>codigo</c>, <c>declaracao</c> e
    /// <c>ativo</c> são ignorados.
    /// </summary>
    [HttpPut("admin/modelos-formulario/{id:guid}")]
    [EndpointName("AlterarModeloDeFormulario")]
    [ExigePermissao<PermissaoConfiguracao>(PermissaoConfiguracao.ModelosFormularioManter, ResourceTipo = ResourceTiposConfiguracao.ModeloFormulario)]
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
            new AtualizarModeloFormularioCommand(id, request.Descritivo, request.Conteudo),
            cancellationToken);
    }

    /// <summary>Reativa o modelo, que volta à escolha dos processos novos.</summary>
    [HttpPost("admin/modelos-formulario/{id:guid}/ativacao")]
    [EndpointName("ReativarModeloDeFormulario")]
    [ExigePermissao<PermissaoConfiguracao>(PermissaoConfiguracao.ModelosFormularioManter, ResourceTipo = ResourceTiposConfiguracao.ModeloFormulario)]
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
    [EndpointName("DesativarModeloDeFormulario")]
    [ExigePermissao<PermissaoConfiguracao>(PermissaoConfiguracao.ModelosFormularioManter, ResourceTipo = ResourceTiposConfiguracao.ModeloFormulario)]
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
