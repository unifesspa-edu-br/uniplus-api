namespace Unifesspa.UniPlus.Configuracao.API.Controllers;

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Queries.Vocabularios;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;

/// <summary>
/// Vocabulários fechados de <c>TipoBanca</c> e <c>FaseCanonica</c> (UNI-REQ-0139).
/// </summary>
/// <remarks>
/// <para>
/// Existem para que o cliente descubra os códigos em runtime. Sem eles, cada tela que monta
/// um desses `select` precisa manter sua própria cópia dos tokens, e a cópia envelhece sem
/// avisar — o drift só aparece como requisição recusada, do lado de quem preencheu o
/// formulário corretamente.
/// </para>
/// <para>
/// <b>Somente leitura, e não por falta de tempo.</b> Os dois conjuntos são governados por
/// código: mudam por versão da API, e não por cadastro administrativo. Um CRUD aqui
/// permitiria acrescentar um código que nenhuma guarda de domínio sabe validar.
/// </para>
/// <para>
/// <b>Sem paginação e sem <c>_links</c>.</b> As duas coleções são pequenas e fechadas (seis
/// e dezesseis itens); paginar ou linkar um vocabulário fixo não ajudaria o cliente, só
/// custaria uma navegação a mais.
/// </para>
/// <para>
/// <b>Devolve rótulo, não o cadastro.</b> A rota de <c>FaseCanonica</c> nasce sempre
/// semeada (<c>FaseCanonicaSeed</c>), então <c>GET /fases-canonicas</c> quase sempre já
/// devolveria os dezesseis códigos — mas devolveria o <b>cadastro</b>, que uma
/// remoção lógica esconde e a paginação por cursor obrigaria o cliente a percorrer. O
/// vocabulário aqui é o código fechado, sempre completo, nunca paginado. Para
/// <c>TipoBanca</c>, que não tem seed nenhum, essa diferença é a única fonte possível: uma
/// base nova devolveria zero linhas em <c>GET /tipos-banca</c>.
/// </para>
/// <para>
/// <b>Rótulo do vocabulário ≠ <c>Nome</c> do cadastro.</b> O rótulo aqui é fixo por código.
/// Depois que um operador renomear uma instância cadastrada, o vocabulário continua
/// anunciando o rótulo canônico — é o comportamento esperado, não um bug.
/// </para>
/// </remarks>
[ApiController]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public.")]
public sealed class VocabulariosController : ControllerBase
{
    private readonly IQueryBus _queryBus;

    public VocabulariosController(IQueryBus queryBus)
    {
        _queryBus = queryBus;
    }

    /// <summary>Lista os seis tipos de banca do conjunto canônico, com código e rótulo.</summary>
    [HttpGet("vocabularios/tipos-banca")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "codigo-tipo-banca", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<TipoBancaVocabularioDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ListarCodigosTipoBanca(CancellationToken cancellationToken)
    {
        IReadOnlyList<TipoBancaVocabularioDto> codigos = await _queryBus
            .Send(new ListarCodigosTipoBancaQuery(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(codigos);
    }

    /// <summary>Lista as dezesseis fases do conjunto canônico, com código e rótulo.</summary>
    [HttpGet("vocabularios/fases-canonicas")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "codigo-fase-canonica", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<FaseCanonicaVocabularioDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ListarCodigosFaseCanonica(CancellationToken cancellationToken)
    {
        IReadOnlyList<FaseCanonicaVocabularioDto> codigos = await _queryBus
            .Send(new ListarCodigosFaseCanonicaQuery(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(codigos);
    }
}
