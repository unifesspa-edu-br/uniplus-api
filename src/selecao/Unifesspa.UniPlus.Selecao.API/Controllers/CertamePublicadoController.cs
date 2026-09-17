namespace Unifesspa.UniPlus.Selecao.API.Controllers;

using System.Diagnostics.CodeAnalysis;

using Application.DTOs;
using Application.Queries.ProcessosSeletivos;

using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
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
    private const string RecursoDaVitrine = "certames";

    private readonly IQueryBus _queryBus;
    private readonly IDomainErrorMapper _mapper;

    private readonly TimeProvider _relogio;

    public CertamePublicadoController(IQueryBus queryBus, IDomainErrorMapper mapper, TimeProvider relogio)
    {
        _queryBus = queryBus;
        _mapper = mapper;
        _relogio = relogio;
    }

    /// <summary>
    /// Vitrine pública: os certames publicados, ordenados por urgência — quem ainda recebe inscrição
    /// primeiro, do prazo mais próximo ao mais distante, e os encerrados depois.
    /// </summary>
    /// <remarks>
    /// <b>A página pode vir menor que o limite pedido, inclusive vazia com continuação disponível.</b>
    /// A visibilidade exige ato normativo registrado, que vive fora deste módulo: a ordenação e o
    /// corte da página acontecem no banco, e o descarte de quem ainda não tem ato acontece depois.
    /// Quem navega deve seguir a âncora de continuação, nunca concluir fim de coleção por página
    /// vazia. O descarte é raro por construção — só alcança certame entre a publicação e o registro
    /// do ato, ou cuja publicação teve o registro recusado.
    /// </remarks>
    [HttpGet("certames")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "certame", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<CertameNaVitrineDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ListarVitrine(
        [FromCursor(RecursoDaVitrine)] PageRequest page,
        [FromQuery(Name = "situacao")] SituacaoDoCertame situacao,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);

        ListarCertamesPublicadosResult resultado = await _queryBus
            .Send(
                new ListarCertamesPublicadosQuery(
                    _relogio.GetUtcNow(), situacao, page.AfterSortKey, page.AfterId, page.Limit, page.Direction),
                cancellationToken)
            .ConfigureAwait(false);

        // Mesma revalidação obrigatória do detalhe, e pelo mesmo motivo: o endereço de uma página da
        // vitrine não muda quando uma publicação insere ou uma retificação reposiciona um certame
        // nela, de modo que uma representação guardada continuaria omitindo o que já vigora.
        Response.Headers.CacheControl = "no-cache";

        return await this.OkPaginatedOrdenadoAsync(
            resultado.Items, resultado.Anterior, resultado.Proximo, page, RecursoDaVitrine,
            cancellationToken: cancellationToken).ConfigureAwait(false);
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
    [HttpGet("certames/{id:guid}")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "certame", Versions = [1])]
    [ProducesResponseType(typeof(CertamePublicadoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ObterCertamePublicado(
        Guid id,
        [FromHeader(Name = "If-None-Match")] string? ifNoneMatch,
        CancellationToken cancellationToken)
    {
        Result<CertamePublicadoDto> resultado = await _queryBus
            .Send(new ObterCertamePublicadoQuery(id), cancellationToken)
            .ConfigureAwait(false);

        // Revalidação OBRIGATÓRIA, não cache proibido: o cliente pode guardar, mas precisa
        // confirmar antes de usar. O endereço da página não muda quando o certame é retificado, de
        // modo que uma representação já guardada na borda ou no navegador seria servida sem que
        // ninguém consultasse a origem — e é na confirmação que o selo faz o seu trabalho.
        //
        // A diretiva é escrita ANTES de ramificar porque a recusa também precisa dela: sem
        // diretiva alguma, um cache compartilhado pode atribuir frescor heurístico ao 404
        // (RFC 9111 §4.2.2) e continuar servindo-o depois de o certame se tornar visível.
        Response.Headers.CacheControl = "no-cache";

        if (resultado.IsFailure)
        {
            return resultado.ToActionResult(_mapper);
        }

        CertamePublicadoDto certame = resultado.Value!;
        string etag = $"\"{certame.VersaoProjecao}:{certame.HashConfiguracao}\"";

        Response.Headers.ETag = etag;

        return SeloDeEntidade.IfNoneMatchCoincide(ifNoneMatch, etag)
            ? StatusCode(StatusCodes.Status304NotModified)
            : Ok(certame);
    }
}
