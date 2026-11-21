namespace Unifesspa.UniPlus.Configuracao.API.Controllers;

using System.Collections.Generic;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Queries.Vocabularios;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;

/// <summary>
/// Vocabulário fechado dos grupos de área do ENEM, usado pelos cadastros de cursos e de Pesos por Área.
/// Somente leitura: o conjunto é governado por código e muda por versão da API, não por cadastro
/// administrativo. Sem paginação, sem <c>_links</c> e sem cache.
/// </summary>
[ApiController]
[Tags("Vocabularios")]
public sealed class VocabulariosDoCursoController : ControllerBase
{
    private readonly IQueryBus _queryBus;

    public VocabulariosDoCursoController(IQueryBus queryBus)
    {
        _queryBus = queryBus;
    }

    /// <summary>
    /// Lista os quatro grupos de área do ENEM (Anexo I da Resolução nº 805/2024/Consepe),
    /// com código e rótulo, na ordem de exibição — para os formulários de cursos e de
    /// Pesos por Área montarem o `select` sem cópia própria dos grupos.
    /// </summary>
    [HttpGet("vocabularios/grupos-area-enem")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "codigo-grupo-area-enem", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<GrupoAreaEnemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ListarGruposAreaEnem(CancellationToken cancellationToken)
    {
        IReadOnlyList<GrupoAreaEnemDto> grupos = await _queryBus
            .Send(new ListarGruposAreaEnemQuery(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(grupos);
    }
}
