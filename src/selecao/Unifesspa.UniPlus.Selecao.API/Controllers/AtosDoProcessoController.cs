namespace Unifesspa.UniPlus.Selecao.API.Controllers;

using System.Diagnostics.CodeAnalysis;

using Application.DTOs;
using Application.Queries.AtosDoProcesso;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Atos do processo seletivo na visão administrativa: os que criaram versão do edital e os registros
/// dos que não criam, com a situação do registro e os documentos de cada um.
/// </summary>
[ApiController]
[Route("processos-seletivos/{processoSeletivoId:guid}/atos")]
// Mesma justificativa de ProcessoSeletivoController: fluxo administrativo de publicação, sem
// fallback policy — restrito a plataforma-admin.
[Authorize(Roles = "plataforma-admin")]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public; sem isso o MVC ignora a classe e nenhum endpoint é registrado.")]
public sealed class AtosDoProcessoController : ControllerBase
{
    private readonly IQueryBus _queryBus;
    private readonly IDomainErrorMapper _mapper;

    public AtosDoProcessoController(IQueryBus queryBus, IDomainErrorMapper mapper)
    {
        _queryBus = queryBus;
        _mapper = mapper;
    }

    /// <summary>
    /// Lista os atos do processo, do mais recente para o mais antigo. O ato pendente há mais tempo
    /// que o limiar configurado vem com <c>pendenciaAcimaDoLimiar</c>: o Publicações não responde a
    /// recusa do registro, então é por aqui que a pendência chega a quem a resolve.
    /// </summary>
    [HttpGet]
    [VendorMediaType(Resource = "ato-do-processo", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<AtoDoProcessoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> Listar(Guid processoSeletivoId, CancellationToken cancellationToken)
    {
        Result<ListarAtosDoProcessoResult> resultado = await _queryBus
            .Send(new ListarAtosDoProcessoQuery(processoSeletivoId), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? Ok(resultado.Value!.Items) : resultado.ToActionResult(_mapper);
    }
}
