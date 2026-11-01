namespace Unifesspa.UniPlus.Autorizacao.API.Controllers;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Permissoes;
using Unifesspa.UniPlus.Infrastructure.Core.Authorization;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;

/// <summary>
/// O catálogo de permissões e as permissões efetivas de quem consulta (ADR-0151), sob <c>/api/autorizacao/permissoes</c>.
/// </summary>
[ApiController]
[Authorize]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public.")]
public sealed class PermissoesController : ControllerBase
{
    private readonly CatalogoDaAplicacao _catalogo;
    private readonly IPermissoesEfetivas _efetivas;

    public PermissoesController(CatalogoDaAplicacao catalogo, IPermissoesEfetivas efetivas)
    {
        _catalogo = catalogo;
        _efetivas = efetivas;
    }

    /// <summary>
    /// Permissões da plataforma agrupadas por módulo e pelo alvo de cada permissão, com código, nome, descrição e
    /// sensibilidade. Exige a permissão <c>autorizacao:permissoes:consultar-catalogo</c>.
    /// </summary>
    [HttpGet("permissoes")]
    [EndpointName("ConsultarCatalogoDePermissoes")]
    [Tags("Autorização")]
    [EndpointSummary("Lista o catálogo de permissões")]
    [EndpointDescription("Permissões da plataforma agrupadas por módulo e pelo alvo de cada permissão, com código, nome, descrição e sensibilidade. Exige a permissão autorizacao:permissoes:consultar-catalogo.")]
    [ExigePermissao<PermissaoAutorizacao>(PermissaoAutorizacao.ConsultarCatalogo)]
    [VendorMediaType(Resource = "permissao-catalogo", Versions = [1])]
    [ProducesResponseType(typeof(IReadOnlyList<ModuloDoCatalogoResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public IActionResult ConsultarCatalogo()
    {
        IReadOnlyList<ModuloDoCatalogoResponse> corpo =
        [
            .. _catalogo.Definicoes
                .GroupBy(static d => d.Modulo)
                .Select(static modulo => new ModuloDoCatalogoResponse(
                    modulo.Key,
                    [.. modulo
                        .GroupBy(static d => d.Resource)
                        .Select(static resource => new ResourceDoCatalogoResponse(
                            resource.Key,
                            [.. resource.Select(static d => new PermissaoDoCatalogoResponse(
                                d.Codigo,
                                d.Nome,
                                d.Descricao,
                                ValoresCanonicos.De(d.Requisito.Sensibilidade)))]))])),
        ];

        return Ok(corpo);
    }

    /// <summary>
    /// Código e nome de cada permissão que o token concede. Informa o que pode aparecer na interface; a decisão de acesso
    /// continua sendo tomada em cada requisição.
    /// </summary>
    [HttpGet("permissoes/efetivas")]
    [EndpointName("ConsultarPermissoesEfetivas")]
    [Tags("Autorização")]
    [EndpointSummary("Lista as permissões efetivas do usuário logado")]
    [EndpointDescription("Código e nome de cada permissão que o token concede. Informa o que pode aparecer na interface; a decisão de acesso continua sendo tomada em cada requisição.")]
    [VendorMediaType(Resource = "permissao-efetiva", Versions = [1])]
    [ProducesResponseType(typeof(IReadOnlyList<PermissaoEfetivaResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ConsultarEfetivas(CancellationToken cancellationToken)
    {
        IReadOnlyList<DefinicaoDePermissao>? permissoes = await _efetivas.ObterAsync(cancellationToken).ConfigureAwait(false);

        return permissoes is null
            ? Challenge()
            : Ok(permissoes.Select(static d => new PermissaoEfetivaResponse(d.Codigo, d.Nome)).ToArray());
    }
}
