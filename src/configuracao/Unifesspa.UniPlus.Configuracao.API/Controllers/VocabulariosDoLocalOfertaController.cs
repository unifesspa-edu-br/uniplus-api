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
/// Vocabulário fechado que o local de oferta referencia por código: o tipo do local.
/// </summary>
/// <remarks>
/// <para>
/// Existe para que o cliente descubra os códigos e os rótulos em runtime, sem manter cópia própria. É somente leitura:
/// muda por versão da API, com a regra que o acompanha, e não por cadastro.
/// </para>
/// <para>
/// A leitura é anônima porque a dos locais de oferta, que carregam esse campo, já é anônima. O vocabulário não traz dado
/// de pessoa.
/// </para>
/// </remarks>
[ApiController]
[Tags("Vocabularios")]
public sealed class VocabulariosDoLocalOfertaController : ControllerBase
{
    private readonly IQueryBus _queryBus;

    public VocabulariosDoLocalOfertaController(IQueryBus queryBus)
    {
        _queryBus = queryBus;
    }

    /// <summary>Lista os tipos de local de oferta, com código e rótulo, na ordem de exibição.</summary>
    [HttpGet("vocabularios/tipos-local-oferta")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "codigo-tipo-local-oferta", Versions = [1])]
    [ProducesResponseType(typeof(IReadOnlyList<CodigoComRotulo<TipoLocalOferta>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ListarTiposLocalOferta(CancellationToken cancellationToken)
    {
        IReadOnlyList<CodigoComRotulo<TipoLocalOferta>> tipos = await _queryBus
            .Send(new ListarTiposLocalOfertaQuery(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(tipos);
    }
}
