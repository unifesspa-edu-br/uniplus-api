namespace Unifesspa.UniPlus.Selecao.API.Controllers;

using System.Diagnostics.CodeAnalysis;

using Application.DTOs.NotasDoEnem;
using Application.Queries.NotasDoEnem;

using Domain.Leiautes;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;

/// <summary>
/// Envio dos CPFs da edição do ENEM ao INEP e importação das notas que o INEP devolve. Restrito a
/// quem configura o processo seletivo.
/// </summary>
[ApiController]
[Authorize(Roles = "plataforma-admin")]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public; sem isso o MVC ignora a classe e nenhum endpoint é registrado.")]
public sealed class NotasDoEnemController : ControllerBase
{
    private readonly IQueryBus _queryBus;

    public NotasDoEnemController(IQueryBus queryBus)
    {
        _queryBus = queryBus;
    }

    /// <summary>
    /// Lista os leiautes de arquivo do INEP ativos, para escolher na geração do envio e na
    /// importação do retorno. Sem o filtro <c>tipo</c> (<c>Envio</c> ou <c>Retorno</c>), devolve os dois tipos.
    /// </summary>
    [HttpGet("leiautes-de-arquivo-do-inep")]
    [VendorMediaType(Resource = "leiaute-de-arquivo-do-inep", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<LeiauteDoInepDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ListarLeiautes(
        [FromQuery(Name = "tipo")] TipoDeLeiauteDoInep? tipo,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<LeiauteDoInepDto> leiautes = await _queryBus
            .Send(new ListarLeiautesDoInepQuery(tipo), cancellationToken)
            .ConfigureAwait(false);

        return Ok(leiautes);
    }
}
