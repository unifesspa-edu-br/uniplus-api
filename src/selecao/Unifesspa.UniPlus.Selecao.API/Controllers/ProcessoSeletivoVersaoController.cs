namespace Unifesspa.UniPlus.Selecao.API.Controllers;

using System.Diagnostics.CodeAnalysis;

using Application.DTOs;
using Application.Queries.ProcessosSeletivos;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.API.Authorization;

/// <summary>
/// Leitura do snapshot congelado vigente do processo seletivo.
/// </summary>
[ApiController]
[Route("processos-seletivos")]
[Tags("ProcessoSeletivo")]
[Authorize]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public; sem isso o MVC ignora a classe e nenhum endpoint é registrado.")]
public sealed class ProcessoSeletivoVersaoController : ControllerBase
{
    private readonly IQueryBus _queryBus;
    private readonly IDomainErrorMapper _mapper;

    public ProcessoSeletivoVersaoController(IQueryBus queryBus, IDomainErrorMapper mapper)
    {
        _queryBus = queryBus;
        _mapper = mapper;
    }

    /// <summary>
    /// Resolve o snapshot congelado vigente do processo num instante (congelamento dos parâmetros na publicação, UNI-REQ-0019,
    /// ADR-0075/0076/0104): a VERSÃO da configuração de maior
    /// <c>vigente_a_partir_de</c> ≤ o instante, desempatada pelo número da
    /// versão. Quem ordena é o relógio do sistema, não a data que o documento
    /// declara. Quando <paramref name="instante"/> é omitido, usa o relógio do
    /// servidor (ADR-0068). É o contrato de LEITURA que o runtime e os
    /// incrementos downstream consomem — a configuração congelada, não a viva.
    /// 422 (<c>Snapshot.VigenteAusente</c>) quando não há versão vigente ≤ o
    /// instante; 404 quando o processo não existe — nunca retorno silencioso.
    /// </summary>
    [AllowAnonymous]
    [ExigeLeituraDoProcesso]
    [HttpGet("{id:guid}/snapshot-vigente")]
    [VendorMediaType(Resource = "snapshot-vigente-processo-seletivo", Versions = [1])]
    [ProducesResponseType(typeof(SnapshotVigenteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ObterSnapshotVigente(
        Guid id,
        [FromQuery] DateTimeOffset? instante,
        CancellationToken cancellationToken)
    {
        Result<SnapshotVigenteDto> resultado = await _queryBus
            .Send(new LerSnapshotVigenteQuery(id, instante), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(_mapper);
    }
}
