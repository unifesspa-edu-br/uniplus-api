namespace Unifesspa.UniPlus.Selecao.API.Controllers;

using System.Diagnostics.CodeAnalysis;

using Application.DTOs;
using Application.Queries.ProcessosSeletivos;

using Http;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Infrastructure.Core.OpenApi;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Leitura pública do certame publicado — o contrato que o portal do candidato e qualquer outro
/// consumidor leem para montar a página de um processo seletivo.
/// </summary>
/// <remarks>
/// Sem <c>[Authorize]</c> de classe e sem escrita: este controlador é só leitura anônima. A
/// configuração do processo continua sendo escrita pelas rotas administrativas, e a leitura
/// administrativa do processo em rascunho continua restrita — esta rota não a afrouxa, serve outro
/// recurso, projetado da versão congelada.
/// </remarks>
[ApiController]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public; sem isso o MVC ignora a classe e nenhum endpoint é registrado.")]
public sealed class CertamePublicadoController : ControllerBase
{
    private readonly IQueryBus _queryBus;
    private readonly IDomainErrorMapper _mapper;

    public CertamePublicadoController(IQueryBus queryBus, IDomainErrorMapper mapper)
    {
        _queryBus = queryBus;
        _mapper = mapper;
    }

    /// <summary>
    /// Certame publicado, projetado da versão de configuração vigente e visível apenas quando o ato
    /// normativo que a criou está registrado.
    /// </summary>
    /// <remarks>
    /// <c>404</c> cobre, com a mesma resposta, o processo inexistente, o processo em rascunho, o
    /// processo sem versão vigente e aquele cujo ato ainda não foi registrado — inclusive quando o
    /// registro foi recusado. A resposta não distingue os casos de propósito: para um chamador
    /// anônimo, distinguir seria responder "esse identificador é um rascunho?".
    /// </remarks>
    [HttpGet("processos-seletivos/{id:guid}/certame")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "certame", Versions = [1])]
    [ProducesResponseType(typeof(CertamePublicadoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ObterCertamePublicado(Guid id, CancellationToken cancellationToken)
    {
        Result<CertamePublicadoDto> resultado = await _queryBus
            .Send(new ObterCertamePublicadoQuery(id), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(_mapper);
    }
}
