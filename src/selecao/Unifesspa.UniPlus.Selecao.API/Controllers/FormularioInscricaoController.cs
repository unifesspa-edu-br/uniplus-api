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
    /// coletados com sua apresentação, projetados da <c>VersaoConfiguracao</c> vigente (nunca da
    /// raiz viva, mesmo sob retificação aberta). <c>422 Snapshot.VigenteAusente</c> quando o
    /// processo existe mas não tem versão vigente — mesmo contrato de erro do endpoint interno
    /// equivalente (<c>ObterSnapshotVigente</c>).
    /// </summary>
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
