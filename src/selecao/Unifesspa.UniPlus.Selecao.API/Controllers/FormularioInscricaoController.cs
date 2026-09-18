namespace Unifesspa.UniPlus.Selecao.API.Controllers;

using System.Diagnostics.CodeAnalysis;

using Application.Commands.ProcessosSeletivos;
using Application.DTOs;
using Application.Queries.ProcessosSeletivos;

using Contracts.Requests;

using Http;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Infrastructure.Core.OpenApi;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Formulário de inscrição (Story #559, UNI-REQ-0017): leitura pública, escrita
/// administrativa — mesmo padrão path-based de <see cref="ObrigatoriedadeLegalController"/>
/// (ADR-0064). Sem <c>[Authorize]</c> de classe: a leitura é anônima, a escrita exige o role
/// <c>plataforma-admin</c> declarado na própria action — o host não tem fallback policy, então
/// omitir a anotação nasceria o endpoint anônimo por omissão.
/// </summary>
[ApiController]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public; sem isso o MVC ignora a classe e nenhum endpoint é registrado.")]
public sealed class FormularioInscricaoController : ControllerBase
{
    private readonly ICommandBus _commandBus;
    private readonly IQueryBus _queryBus;
    private readonly IDomainErrorMapper _mapper;

    public FormularioInscricaoController(ICommandBus commandBus, IQueryBus queryBus, IDomainErrorMapper mapper)
    {
        _commandBus = commandBus;
        _queryBus = queryBus;
        _mapper = mapper;
    }

    /// <summary>
    /// Renderização pública do formulário de inscrição — título, termo de aceite e os fatos
    /// coletados com sua apresentação, projetados da versão que o certame <b>divulgado</b> serve.
    /// </summary>
    /// <remarks>
    /// <para>
    /// É a MESMA versão que <c>GET /certames/{id}</c> serve, e não a mais nova por relógio.
    /// Enquanto o ato de uma retificação não se confirma, ela não tem publicidade: servir o
    /// formulário dela faria o candidato ler um edital e preencher o de outro, e coletaria dados
    /// sob uma configuração que pode nunca vir a ter ato normativo.
    /// </para>
    /// <para>
    /// <c>404</c> cobre, com a mesma resposta, o processo inexistente, o em rascunho, o sem versão
    /// vigente e o sem divulgação. A resposta não os distingue de propósito: para um chamador
    /// anônimo, distinguir seria responder "esse identificador é um rascunho?".
    /// </para>
    /// </remarks>
    [HttpGet("processos-seletivos/{id:guid}/formulario")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "formulario-inscricao", Versions = [1])]
    [ProducesResponseType(typeof(FormularioRenderizavelDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ObterFormularioRenderizavel(Guid id, CancellationToken cancellationToken)
    {
        Result<FormularioRenderizavelDto> resultado = await _queryBus
            .Send(new ObterFormularioRenderizavelQuery(id), cancellationToken)
            .ConfigureAwait(false);

        // Revalidação obrigatória, não cache proibido: o endereço não muda quando a divulgação
        // chega nem quando o certame é retificado. Escrita ANTES de ramificar porque a recusa é
        // que precisa dela — o 404 enquanto a divulgação não materializa é transitório, e sem
        // diretiva um cache compartilhado lhe atribui frescor heurístico (RFC 9111 §4.2.2) e
        // segue escondendo o formulário depois que ele passa a existir.
        Response.Headers.CacheControl = "no-cache";

        return resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Define (ou substitui) título e termo de aceite do formulário de inscrição. Restrito ao
    /// role <c>plataforma-admin</c>. Bloqueado em processo publicado sem retificação aberta —
    /// mesmo padrão dos demais <c>Definir*</c> de <see cref="ProcessoSeletivoController"/>.
    /// </summary>
    [HttpPut("admin/processos-seletivos/{id:guid}/formulario")]
    [Authorize(Roles = "plataforma-admin")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status428PreconditionRequired)]
    [EmiteETag]
    public async Task<IActionResult> DefinirFormulario(
        Guid id,
        [FromBody] DefinirFormularioRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!TentarLerPrecondicao(ifMatch, out PrecondicaoIfMatch precondicao, out IActionResult? malformada))
            return malformada!;

        Result<MutacaoAceita> resultado = await _commandBus.Send(
            new DefinirFormularioCommand(id, request.Titulo, request.TermoAceiteTexto, precondicao), cancellationToken);
        return ResponderMutacao(resultado);
    }

    private bool TentarLerPrecondicao(string? ifMatch, out PrecondicaoIfMatch precondicao, out IActionResult? malformada)
    {
        Result<PrecondicaoIfMatch> analise = IfMatchHeader.Analisar(ifMatch);
        if (analise.IsFailure)
        {
            precondicao = PrecondicaoIfMatch.Ausente;
            malformada = analise.ToActionResult(_mapper);
            return false;
        }

        precondicao = analise.Value!;
        malformada = null;
        return true;
    }

    /// <summary>
    /// 204 com o <c>ETag</c> <b>novo</b> quando a mutação correu sob sessão editorial; 204 nu
    /// quando o processo está em rascunho — mesmo padrão de
    /// <see cref="ProcessoSeletivoController"/>.
    /// </summary>
    private IActionResult ResponderMutacao(Result<MutacaoAceita> resultado)
    {
        if (resultado.IsFailure)
        {
            return resultado.ToActionResult(_mapper);
        }

        if (resultado.Value!.ETag is { } etag)
        {
            Response.Headers.ETag = etag;
        }

        return NoContent();
    }
}
