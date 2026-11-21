namespace Unifesspa.UniPlus.Configuracao.API.Controllers;

using System.Collections.Generic;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Application.Queries.Vocabularios;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;

/// <summary>
/// Vocabulários fechados que a oferta de curso referencia por código: o programa de oferta e o formato pedagógico.
/// </summary>
/// <remarks>
/// <para>
/// Existem para que o cliente descubra os códigos e os rótulos em runtime, sem manter cópia própria. São somente leitura:
/// mudam por versão da API, com a regra que os acompanha, e não por cadastro.
/// </para>
/// <para>
/// A leitura é anônima porque a das ofertas de curso, que carregam esses dois campos, já é anônima, e o edital publica o
/// que elas descrevem. O vocabulário não traz dado de pessoa.
/// </para>
/// </remarks>
[ApiController]
[Tags("Vocabularios")]
public sealed class VocabulariosDaOfertaDeCursoController : ControllerBase
{
    private readonly IQueryBus _queryBus;

    public VocabulariosDaOfertaDeCursoController(IQueryBus queryBus)
    {
        _queryBus = queryBus;
    }

    /// <summary>Lista os programas de oferta, com código e rótulo, na ordem de exibição.</summary>
    [HttpGet("vocabularios/programas-oferta")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "codigo-programa-de-oferta", Versions = [1])]
    [ProducesResponseType(typeof(IReadOnlyList<CodigoComRotulo<ProgramaDeOferta>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ListarProgramasDeOferta(CancellationToken cancellationToken)
    {
        IReadOnlyList<CodigoComRotulo<ProgramaDeOferta>> programas = await _queryBus
            .Send(new ListarProgramasDeOfertaQuery(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(programas);
    }

    /// <summary>Lista os formatos pedagógicos, com código e rótulo, na ordem de exibição.</summary>
    [HttpGet("vocabularios/formatos-pedagogicos")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "codigo-formato-pedagogico", Versions = [1])]
    [ProducesResponseType(typeof(IReadOnlyList<CodigoComRotulo<FormatoPedagogico>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ListarFormatosPedagogicos(CancellationToken cancellationToken)
    {
        IReadOnlyList<CodigoComRotulo<FormatoPedagogico>> formatos = await _queryBus
            .Send(new ListarFormatosPedagogicosQuery(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(formatos);
    }
}
