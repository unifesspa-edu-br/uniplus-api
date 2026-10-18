namespace Unifesspa.UniPlus.Selecao.API.Controllers;

using System.Diagnostics.CodeAnalysis;

using Application.DTOs;
using Application.Queries.ProdutosPrevistos;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Os itens que o edital vigente declara publicar em cada fase e etapa do cronograma, com a situação
/// de cada um na visão administrativa.
/// </summary>
[ApiController]
[Route("processos-seletivos/{processoSeletivoId:guid}/produtos-previstos")]
// Mesma justificativa de ProcessoSeletivoController: fluxo administrativo de publicação, sem
// fallback policy — restrito a plataforma-admin.
[Authorize(Roles = "plataforma-admin")]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public; sem isso o MVC ignora a classe e nenhum endpoint é registrado.")]
public sealed class ProdutosPrevistosDoProcessoController : ControllerBase
{
    private readonly IQueryBus _queryBus;
    private readonly IDomainErrorMapper _mapper;

    public ProdutosPrevistosDoProcessoController(IQueryBus queryBus, IDomainErrorMapper mapper)
    {
        _queryBus = queryBus;
        _mapper = mapper;
    }

    /// <summary>
    /// Lista os itens previstos no edital vigente, agrupados pela fase do cronograma. Cada item diz
    /// se está aberto, pendente ou publicado e, quando não pode ser publicado agora, o motivo.
    /// </summary>
    [HttpGet]
    [VendorMediaType(Resource = "produtos-previstos-do-processo", Versions = [1])]
    [ProducesResponseType(typeof(ProdutosPrevistosDoProcessoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Listar(Guid processoSeletivoId, CancellationToken cancellationToken)
    {
        Result<ProdutosPrevistosDoProcessoDto> resultado = await _queryBus
            .Send(new ListarProdutosPrevistosDoProcessoQuery(processoSeletivoId), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(_mapper);
    }
}
