namespace Unifesspa.UniPlus.Configuracao.API.Controllers;

using System.Collections.Generic;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Application.Queries.Vocabularios;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// Vocabulários fechados da banca (UNI-REQ-0139): os tipos de banca, os papéis que um tipo de
/// banca pode ter e os tipos de decisão que ele pode julgar. Somente leitura: os conjuntos são
/// governados por código e mudam por versão da API, não por cadastro administrativo. Sem
/// paginação, sem <c>_links</c> e sem cache.
/// </summary>
/// <remarks>
/// O vocabulário é o conjunto fechado, sempre completo, e não o cadastro de tipos de banca: a
/// administração pode remover um tipo semeado, e <c>GET /tipos-banca</c> devolveria só o que
/// ficou vivo. O rótulo aqui é fixo por código e não acompanha o <c>Nome</c> renomeado no cadastro.
/// </remarks>
[ApiController]
[Tags("Vocabularios")]
public sealed class VocabulariosDaBancaController : ControllerBase
{
    private readonly IQueryBus _queryBus;

    public VocabulariosDaBancaController(IQueryBus queryBus)
    {
        _queryBus = queryBus;
    }

    /// <summary>Lista os tipos de banca do vocabulário, com código e rótulo, na ordem de exibição.</summary>
    [HttpGet("vocabularios/tipos-banca")]
    [EndpointName("ListarCodigosDeTipoDeBanca")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "codigo-tipo-banca", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<CodigoComRotulo<TipoDeBanca>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ListarCodigosTipoBanca(CancellationToken cancellationToken)
    {
        IReadOnlyList<CodigoComRotulo<TipoDeBanca>> codigos = await _queryBus
            .Send(new ListarCodigosTipoBancaQuery(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(codigos);
    }

    /// <summary>Lista os papéis que um tipo de banca pode ter, com código e rótulo, na ordem de exibição.</summary>
    [HttpGet("vocabularios/papeis-de-banca")]
    [EndpointName("ListarPapeisDeBanca")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "codigo-papel-de-banca", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<CodigoComRotulo<PapelDaBanca>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ListarPapeisDeBanca(CancellationToken cancellationToken)
    {
        IReadOnlyList<CodigoComRotulo<PapelDaBanca>> papeis = await _queryBus
            .Send(new ListarPapeisDeBancaQuery(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(papeis);
    }

    /// <summary>Lista os tipos de decisão que um tipo de banca pode julgar, com código e rótulo, na ordem de exibição.</summary>
    [HttpGet("vocabularios/tipos-de-decisao")]
    [EndpointName("ListarTiposDeDecisao")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "codigo-tipo-de-decisao", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<CodigoComRotulo<TipoMotivoDeDecisao>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ListarTiposDeDecisao(CancellationToken cancellationToken)
    {
        IReadOnlyList<CodigoComRotulo<TipoMotivoDeDecisao>> tipos = await _queryBus
            .Send(new ListarTiposDeDecisaoQuery(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(tipos);
    }
}
