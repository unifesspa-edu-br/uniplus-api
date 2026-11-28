namespace Unifesspa.UniPlus.Selecao.API.Controllers;

using Application.Commands.ProcessosSeletivos;

using Contracts.Requests;

using Http;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Infrastructure.Core.OpenApi;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.API.Authorization;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;

/// <summary>
/// Declaração da importação do resultado do processo seletivo de resultado importado: os relatórios que ele importa e a primeira
/// fase depois da importação.
/// </summary>
[ApiController]
[EmiteEsperaEsgotadaPelaTrava]
[Route("processos-seletivos")]
[Tags("ProcessoSeletivo")]
[Authorize]
public sealed class ProcessoSeletivoImportacaoDoResultadoController : ControllerBase
{
    private readonly ICommandBus _commandBus;
    private readonly IDomainErrorMapper _mapper;

    public ProcessoSeletivoImportacaoDoResultadoController(
        ICommandBus commandBus,
        IDomainErrorMapper mapper)
    {
        _commandBus = commandBus;
        _mapper = mapper;
    }

    /// <summary>
    /// Substitui a declaração da importação do resultado: cada relatório pelo código e pela versão do leiaute, sem repetir leiaute, e a
    /// primeira fase depois da importação, que é fase do cronograma do processo. A fase omitida ou nula apaga a declaração da fase, e a
    /// lista vazia com a fase omitida volta o processo a por declarar. O processo com inscrição pelo sistema não declara importação.
    /// A declaração é obrigatória para publicar o processo de resultado importado.
    /// </summary>
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosConfigurar)]
    [HttpPut("{id:guid}/importacao-resultado")]
    [EndpointName("DefinirImportacaoDoResultado")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> DefinirImportacaoDoResultado(Guid id, [FromBody] DefinirImportacaoDoResultadoRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        Result resultado = await _commandBus.Send(
            new DefinirImportacaoDoResultadoCommand(id, request.Relatorios, request.PrimeiraFaseDepoisDaImportacaoId), cancellationToken);
        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }
}
