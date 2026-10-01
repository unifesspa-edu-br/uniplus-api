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
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Formulários do processo por finalidade (UNI-REQ-0144): leitura pública e escrita
/// administrativa, com a finalidade no caminho (<c>INSCRICAO</c>, <c>ISENCAO_TAXA</c>,
/// <c>HABILITACAO</c>). Sem <c>[Authorize]</c> de classe: a leitura é anônima, e a escrita exige o
/// papel <c>plataforma-admin</c> declarado em cada action — o host não tem fallback policy, então
/// omitir a anotação nasceria o endpoint anônimo por omissão.
/// </summary>
[ApiController]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public; sem isso o MVC ignora a classe e nenhum endpoint é registrado.")]
public sealed class FormulariosController : ControllerBase
{
    private readonly ICommandBus _commandBus;
    private readonly IQueryBus _queryBus;
    private readonly IDomainErrorMapper _mapper;

    public FormulariosController(ICommandBus commandBus, IQueryBus queryBus, IDomainErrorMapper mapper)
    {
        _commandBus = commandBus;
        _queryBus = queryBus;
        _mapper = mapper;
    }

    /// <summary>
    /// Renderização pública do formulário de uma finalidade — título, etapas, termos e os fatos
    /// coletados com sua apresentação, projetados da versão que o certame <b>divulgado</b> serve.
    /// </summary>
    /// <remarks>
    /// <para>
    /// É a MESMA versão que <c>GET /certames/{id}</c> serve, e não a mais nova por relógio.
    /// Enquanto o ato de uma retificação não se confirma, ela não tem publicidade: servir o
    /// formulário dela faria o candidato ler um edital e preencher o de outro.
    /// </para>
    /// <para>
    /// <c>404</c> cobre, com a mesma resposta, o processo inexistente, o em rascunho, o sem versão
    /// vigente, o sem divulgação, a finalidade desconhecida e o processo sem formulário da
    /// finalidade. Para um chamador anônimo, distinguir seria responder "esse identificador é um
    /// rascunho?".
    /// </para>
    /// </remarks>
    [HttpGet("processos-seletivos/{id:guid}/formularios/{finalidade}")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "formulario", Versions = [1])]
    [ProducesResponseType(typeof(FormularioRenderizavelDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ObterFormularioRenderizavel(Guid id, string finalidade, CancellationToken cancellationToken)
    {
        // Revalidação obrigatória, não cache proibido: o endereço não muda quando a divulgação
        // chega nem quando o certame é retificado, e sem diretiva um cache compartilhado atribui
        // frescor heurístico ao 404 transitório (RFC 9111 §4.2.2).
        Response.Headers.CacheControl = "no-cache";

        if (EstruturaFormulario.FinalidadeDoToken(finalidade) is not (not FinalidadeFormulario.Nenhuma and var alvo))
        {
            return NotFound();
        }

        Result<FormularioRenderizavelDto> resultado = await _queryBus
            .Send(new ObterFormularioRenderizavelQuery(id, alvo), cancellationToken)
            .ConfigureAwait(false);
        return resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Define ou substitui o formulário da finalidade: a fase do cronograma, o título e as etapas.
    /// Editável em rascunho e sob sessão de retificação, que pode acrescentar formulário.
    /// </summary>
    [HttpPut("admin/processos-seletivos/{id:guid}/formularios/{finalidade}")]
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
    public Task<IActionResult> DefinirFormulario(
        Guid id,
        string finalidade,
        [FromBody] DefinirFormularioRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return EnviarMutacao(finalidade, ifMatch,
            (alvo, precondicao) => new DefinirFormularioCommand(id, alvo, request.FaseId, request.Titulo, request.Etapas, precondicao),
            cancellationToken);
    }

    /// <summary>Remove o formulário da finalidade, com os itens e os termos dele. Só em rascunho.</summary>
    [HttpDelete("admin/processos-seletivos/{id:guid}/formularios/{finalidade}")]
    [Authorize(Roles = "plataforma-admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> RemoverFormulario(Guid id, string finalidade, CancellationToken cancellationToken) =>
        EnviarMutacao(finalidade, ifMatch: null,
            (alvo, precondicao) => new RemoverFormularioCommand(id, alvo, precondicao),
            cancellationToken);

    /// <summary>
    /// Substitui os itens do formulário da finalidade: os fatos que ele coleta, com a ordem, a seção
    /// e a pré-condição. Cada fato é coletado por um só formulário do processo.
    /// </summary>
    [HttpPut("admin/processos-seletivos/{id:guid}/formularios/{finalidade}/itens")]
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
    public Task<IActionResult> DefinirItens(
        Guid id,
        string finalidade,
        [FromBody] DefinirItensDoFormularioRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return EnviarMutacao(finalidade, ifMatch,
            (alvo, precondicao) => new DefinirFatosColetadosCommand(id, alvo, request.Itens, precondicao),
            cancellationToken);
    }

    /// <summary>
    /// Substitui os termos de consentimento que o formulário da finalidade exige, escolhidos no
    /// catálogo por termo e versão (UNI-REQ-0086).
    /// </summary>
    [HttpPut("admin/processos-seletivos/{id:guid}/formularios/{finalidade}/termos")]
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
    public Task<IActionResult> DefinirTermos(
        Guid id,
        string finalidade,
        [FromBody] DefinirTermosDoFormularioRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return EnviarMutacao(finalidade, ifMatch,
            (alvo, precondicao) => new DefinirTermosDoFormularioCommand(id, alvo, request.Termos, precondicao),
            cancellationToken);
    }

    /// <summary>
    /// Aplica ao processo a cópia de um modelo de formulário: o formulário da finalidade do modelo é
    /// criado ou substituído por inteiro, e a resposta relata o que a cópia trouxe, manteve,
    /// descartou e derivou (UNI-REQ-0144). Só em rascunho, onde o <c>If-Match</c> é aceito e
    /// ignorado, como nas demais escritas; mudar o modelo depois não muda o processo.
    /// </summary>
    [HttpPost("admin/processos-seletivos/{id:guid}/formularios/aplicacoes-de-modelo")]
    [Authorize(Roles = "plataforma-admin")]
    [RequiresIdempotencyKey]
    [VendorMediaType(Resource = "aplicacao-de-modelo-formulario", Versions = [1])]
    [ProducesResponseType(typeof(AplicacaoDeModeloDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AplicarModelo(
        Guid id,
        [FromBody] AplicacaoDeModeloInput request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        Result<PrecondicaoIfMatch> analise = IfMatchHeader.Analisar(ifMatch);
        if (analise.IsFailure)
        {
            return analise.ToActionResult(_mapper);
        }

        Result<AplicacaoDeModeloDto> resultado = await _commandBus
            .Send(new AplicarModeloFormularioCommand(id, request.ModeloId, analise.Value!), cancellationToken).ConfigureAwait(false);
        return resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// O fluxo comum das escritas: finalidade desconhecida é 404, o <c>If-Match</c> malformado é
    /// recusado antes do comando, e a resposta segue o padrão das mutações sob sessão editorial.
    /// </summary>
    private async Task<IActionResult> EnviarMutacao(
        string finalidade,
        string? ifMatch,
        Func<FinalidadeFormulario, PrecondicaoIfMatch, ICommand<Result<MutacaoAceita>>> comando,
        CancellationToken cancellationToken)
    {
        if (EstruturaFormulario.FinalidadeDoToken(finalidade) is not (not FinalidadeFormulario.Nenhuma and var alvo))
        {
            return NotFound();
        }

        Result<PrecondicaoIfMatch> analise = IfMatchHeader.Analisar(ifMatch);
        if (analise.IsFailure)
        {
            return analise.ToActionResult(_mapper);
        }

        Result<MutacaoAceita> resultado = await _commandBus.Send(comando(alvo, analise.Value!), cancellationToken).ConfigureAwait(false);
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
