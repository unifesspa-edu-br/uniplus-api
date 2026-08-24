namespace Unifesspa.UniPlus.Selecao.API.Controllers;

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Infrastructure.Core.Hateoas;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.RolDeRegras;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Leitura do <c>rol_de_regras</c> — a biblioteca de regras tipadas e versionadas que as
/// dimensões da configuração de um Processo Seletivo referenciam por
/// <c>(codigo, versao, hash)</c>.
/// </summary>
/// <remarks>
/// <para>
/// Existe para que o cliente administrativo descubra em runtime quais regras pode referenciar
/// e reencontre a versão exata que um rascunho aponta. Sem isso, cada tela de configuração
/// mantém sua própria lista de fórmulas, precisões, critérios de desempate, algoritmos de
/// contagem — constantes paralelas que envelhecem quando o catálogo ganha uma versão nova, e
/// o desencontro só aparece como referência recusada na publicação.
/// </para>
/// <para>
/// <b>Somente leitura, e por decisão de governança.</b> O catálogo é seed-governado e
/// append-only (ADR-0112): evoluir uma regra é publicar uma versão nova por migration, junto
/// da mudança de comportamento que ela descreve. Não há rota de escrita aqui, e não é
/// pendência — um CRUD permitiria criar uma regra que nenhum motor sabe executar.
/// </para>
/// <para>
/// A leitura fica sob a mesma autorização da configuração do Processo Seletivo que a consome:
/// <c>plataforma-admin</c>. Não é sobre sigilo — a definição de uma regra acaba no edital —, é
/// sobre alcance: quem descobre o catálogo é quem monta uma configuração, e um perfil de
/// candidato autenticado não tem o que fazer com o esquema de argumentos de uma regra de
/// distribuição de vagas. Autenticação sozinha alcançaria justamente esse perfil.
/// </para>
/// </remarks>
[ApiController]
[Authorize(Roles = "plataforma-admin")]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public; sem isso o MVC ignora a classe.")]
public sealed class RegrasCatalogoController : ControllerBase
{
    private const string ResourceTag = "regras-catalogo";

    private readonly IQueryBus _queryBus;
    private readonly IDomainErrorMapper _mapper;
    private readonly IResourceLinksBuilder<RegraCatalogoDto> _linksBuilder;

    public RegrasCatalogoController(
        IQueryBus queryBus,
        IDomainErrorMapper mapper,
        IResourceLinksBuilder<RegraCatalogoDto> linksBuilder)
    {
        _queryBus = queryBus;
        _mapper = mapper;
        _linksBuilder = linksBuilder;
    }

    /// <summary>
    /// Lista o catálogo, paginado por cursor opaco bidirecional (ADR-0026 + ADR-0089), com
    /// filtro opcional por tipo. A ordem é tipo, código e versão.
    /// </summary>
    /// <remarks>
    /// Ordenar por versão não elege a mais recente: as versões coexistem de propósito, e qual
    /// vale para um certame é decisão de quem configura. Comparar <c>v2</c> com <c>v10</c>
    /// lexicalmente responderia errado, e é justamente por isso que a API não responde.
    /// </remarks>
    [HttpGet("regras-catalogo")]
    [VendorMediaType(Resource = "regra-catalogo", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<RegraCatalogoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Listar(
        [FromCursor(ResourceTag)] PageRequest page,
        [FromQuery] string? tipo,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);

        // O filtro chega como texto e passa pelo mesmo mapa canônico que a coluna usa, não por
        // binding direto no enum. Ligado ao enum, o parâmetro aceitaria a grafia dos membros em
        // C# e recusaria justamente o código que esta API devolve em `tipo` — quem relesse o
        // valor de uma resposta e o usasse como filtro receberia 400.
        TipoRegra? tipoFiltrado = null;
        if (!string.IsNullOrWhiteSpace(tipo))
        {
            try
            {
                tipoFiltrado = TipoRegraCodigo.FromCodigo(tipo);
            }
            catch (ArgumentOutOfRangeException)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Tipo de regra desconhecido",
                    Detail = "O filtro aceita apenas os códigos canônicos de tipo de regra do catálogo.",
                    Status = StatusCodes.Status400BadRequest,
                });
            }
        }

        ListarRegrasCatalogoResult resultado = await _queryBus.Send(
            new ListarRegrasCatalogoQuery(tipoFiltrado, page.AfterId, page.Limit, page.Direction),
            cancellationToken).ConfigureAwait(false);

        RegraCatalogoDto[] comLinks =
            [.. resultado.Items.Select(r => r with { Links = _linksBuilder.Build(r) })];

        return await this.OkPaginatedAsync(
            comLinks,
            resultado.AnteriorAfterId,
            resultado.ProximoAfterId,
            page,
            ResourceTag,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Obtém uma entrada pela identidade <c>(codigo, versao)</c> — a mesma que um rascunho
    /// referencia, o que permite reler a definição exata que ele aponta após um refresh.
    /// </summary>
    [HttpGet("regras-catalogo/{codigo}/versoes/{versao}")]
    [VendorMediaType(Resource = "regra-catalogo", Versions = [1])]
    [ProducesResponseType(typeof(RegraCatalogoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ObterPorIdentidade(
        string codigo,
        string versao,
        CancellationToken cancellationToken)
    {
        Result<RegraCatalogoDto> resultado = await _queryBus
            .Send(new ObterRegraCatalogoQuery(codigo, versao), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess
            ? Ok(resultado.Value! with { Links = _linksBuilder.Build(resultado.Value!) })
            : resultado.ToActionResult(_mapper);
    }
}
