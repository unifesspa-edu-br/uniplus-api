namespace Unifesspa.UniPlus.Configuracao.API.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.FatosCandidato;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Permissoes;
using Unifesspa.UniPlus.Infrastructure.Core.Authorization;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Manutenção dos valores de domínio de um fato do candidato cadastrado pelo administrador do cadastro:
/// acréscimo, correção, ordem, desativação e reativação.
/// </summary>
[ApiController]
[Authorize]
[Tags("FatosCandidato")]
public sealed class FatosCandidatoValoresController : ControllerBase
{
    private readonly ICommandBus _commandBus;
    private readonly IDomainErrorMapper _mapper;

    public FatosCandidatoValoresController(ICommandBus commandBus, IDomainErrorMapper mapper)
    {
        _commandBus = commandBus;
        _mapper = mapper;
    }

    /// <summary>Acrescenta um valor a um fato de fonte global do administrador.</summary>
    [HttpPost("admin/fatos-candidato/{id:guid}/valores")]
    [ExigePermissao<PermissaoConfiguracao>(PermissaoConfiguracao.FatosCandidatoManter, ResourceTipo = ResourceTiposConfiguracao.FatoCandidato)]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> AdicionarValor(Guid id, [FromBody] ValorDominioInput request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Enviar(new AdicionarValorDominioCommand(id, request.Codigo, request.Descricao, request.Ordem, request.Orientacao), cancellationToken);
    }

    /// <summary>
    /// Corrige a descrição e a orientação de um valor do fato cadastrado pelo administrador do cadastro. O código do valor
    /// não muda: mudar o sentido de uma opção é acrescentar outro valor e desativar o antigo.
    /// </summary>
    [HttpPut("admin/fatos-candidato/{id:guid}/valores/{codigo}")]
    [ExigePermissao<PermissaoConfiguracao>(PermissaoConfiguracao.FatosCandidatoManter, ResourceTipo = ResourceTiposConfiguracao.FatoCandidato)]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> CorrigirValor(Guid id, string codigo, [FromBody] CorrecaoDeValorDominioInput request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Enviar(new CorrigirValorDominioCommand(id, codigo, request.Descricao, request.Orientacao), cancellationToken);
    }

    /// <summary>
    /// Reordena os valores do fato cadastrado pelo administrador do cadastro pela lista completa de códigos, ativos e
    /// desativados. Fica fora de <c>valores/</c> porque colidiria com um valor de código ORDEM.
    /// </summary>
    [HttpPut("admin/fatos-candidato/{id:guid}/ordem-dos-valores")]
    [ExigePermissao<PermissaoConfiguracao>(PermissaoConfiguracao.FatosCandidatoManter, ResourceTipo = ResourceTiposConfiguracao.FatoCandidato)]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> ReordenarValores(Guid id, [FromBody] OrdemDosValoresInput request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Enviar(new ReordenarValoresDominioCommand(id, request.Codigos), cancellationToken);
    }

    /// <summary>Reativa um valor do fato cadastrado pelo administrador do cadastro.</summary>
    [HttpPost("admin/fatos-candidato/{id:guid}/valores/{codigo}/ativacao")]
    [ExigePermissao<PermissaoConfiguracao>(PermissaoConfiguracao.FatosCandidatoManter, ResourceTipo = ResourceTiposConfiguracao.FatoCandidato)]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> ReativarValor(Guid id, string codigo, CancellationToken cancellationToken) =>
        Enviar(new ReativarValorDominioCommand(id, codigo), cancellationToken);

    /// <summary>
    /// Desativa um valor do fato cadastrado pelo administrador do cadastro: condição nova que o cite é recusada, e a que já o
    /// citava continua. O código do valor nunca é removido nem renomeado.
    /// </summary>
    [HttpDelete("admin/fatos-candidato/{id:guid}/valores/{codigo}")]
    [ExigePermissao<PermissaoConfiguracao>(PermissaoConfiguracao.FatosCandidatoManter, ResourceTipo = ResourceTiposConfiguracao.FatoCandidato)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> DesativarValor(Guid id, string codigo, CancellationToken cancellationToken) =>
        Enviar(new DesativarValorDominioCommand(id, codigo), cancellationToken);

    private async Task<IActionResult> Enviar(ICommand<Result> command, CancellationToken cancellationToken)
    {
        Result resultado = await _commandBus.Send(command, cancellationToken).ConfigureAwait(false);
        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }
}
