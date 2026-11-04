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
using Unifesspa.UniPlus.Selecao.API.Authorization;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Retificação do processo já publicado: o atalho em um ato só e a sessão editorial de retificação, do abrir ao fechar.
/// </summary>
[ApiController]
[Route("processos-seletivos")]
[Tags("ProcessoSeletivo")]
[Authorize]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public; sem isso o MVC ignora a classe e nenhum endpoint é registrado.")]
public sealed class ProcessoSeletivoRetificacaoController : ControllerBase
{
    private readonly ICommandBus _commandBus;
    private readonly IQueryBus _queryBus;
    private readonly IDomainErrorMapper _mapper;

    public ProcessoSeletivoRetificacaoController(
        ICommandBus commandBus,
        IQueryBus queryBus,
        IDomainErrorMapper mapper)
    {
        _commandBus = commandBus;
        _queryBus = queryBus;
        _mapper = mapper;
    }

    /// <summary>
    /// Retifica o processo já publicado (congelamento dos parâmetros na publicação, UNI-REQ-0019, ADR-0101/0103): registra um novo ato,
    /// que emenda o ato criador da versão corrente e a sucede — a versão anterior
    /// permanece imutável. O motivo é obrigatório.
    /// <para>
    /// O corpo é o de <see cref="ProcessoSeletivoPublicacaoController.Publicar"/> mais o <c>motivo</c>: o alvo da retificação
    /// NÃO é informado pelo cliente — o servidor o infere do topo da cadeia de versões
    /// (ADR-0101). E o tipo do ato continua vindo declarado: uma convocação retificada
    /// continua convocação.
    /// </para>
    /// <para>
    /// <b>Com uma sessão editorial aberta, este atalho recusa</b> (409): os dois caminhos
    /// retificam o mesmo ato, e deixá-los correr juntos congelaria a versão N+1 a partir da
    /// configuração que a sessão está no meio de editar. Quem tem sessão aberta a fecha por
    /// <see cref="FecharRetificacao"/>.
    /// </para>
    /// </summary>
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosPublicar)]
    [HttpPost("{id:guid}/retificacoes")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict,
        Description = "Há uma retificação em curso neste processo: enquanto a sessão editorial existir, o atalho em um ato só recusa — fechá-la ou descartá-la libera esta rota. Também responde 409 quando outra publicação ou retificação concorrente congelou uma versão da configuração entre a leitura e a gravação desta (reler o processo e resubmeter), e quando outra requisição com a mesma Idempotency-Key ainda está em processamento.")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Retificar(
        Guid id,
        [FromBody] RetificarProcessoSeletivoRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result resultado = await _commandBus.Send(
            new RetificarProcessoSeletivoCommand(
                id,
                request.Motivo,
                request.Numero,
                request.PeriodoInscricaoInicio,
                request.PeriodoInscricaoFim,
                request.DocumentoEditalId,
                MapeamentoDoAto.ParaDados(request.Ato),
                Anexos: MapeamentoDoAto.ParaAnexos(request.Anexos)),
            cancellationToken);

        if (resultado.IsSuccess)
        {
            return NoContent();
        }

        IActionResult actionResult = resultado.ToActionResult(_mapper);
        return await EnriquecimentoDaPublicacao.EnriquecerComObrigatoriedadesReprovadasAsync(
            _queryBus, resultado, actionResult, id, request.PeriodoInscricaoInicio, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Abre a <b>sessão editorial</b> de retificação (ADR-0110 D3): o certame publicado
    /// volta a aceitar os seis <c>Definir*</c>, <b>sem</b> mudar de status e <b>sem</b>
    /// congelar nada. A versão nova nasce só no fechamento.
    /// </summary>
    /// <remarks>
    /// Devolve o <c>ETag</c> da sessão recém-criada — o cliente já sai apto a mutar, sem um
    /// <c>GET</c> no meio.
    /// </remarks>
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosPublicar)]
    [HttpPost("{id:guid}/retificacao-em-curso")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(typeof(RetificacaoEmCursoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict,
        Description = "Já existe retificação em curso para este processo — só uma por vez. Fechar ou descartar a atual antes de abrir outra. Também responde 409 quando outra requisição com a mesma Idempotency-Key ainda está em processamento.")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [EmiteETag]
    public async Task<IActionResult> AbrirRetificacao(
        Guid id,
        [FromBody] AbrirRetificacaoRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<RetificacaoEmCursoDto> resultado = await _commandBus.Send(
            new AbrirRetificacaoCommand(id, request.Motivo), cancellationToken);

        if (resultado.IsFailure)
        {
            return resultado.ToActionResult(_mapper);
        }

        RetificacaoEmCursoDto rascunho = resultado.Value!;
        Response.Headers.ETag = rascunho.ETag;
        return CreatedAtAction(nameof(ObterRetificacaoEmCurso), new { id }, rascunho);
    }

    /// <summary>
    /// Consulta a sessão editorial em curso — é por aqui que o cliente <b>relê o ETag</b>
    /// depois de um 412.
    /// </summary>
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosConfigurar)]
    [HttpGet("{id:guid}/retificacao-em-curso")]
    [VendorMediaType(Resource = "retificacao-em-curso", Versions = [1])]
    [ProducesResponseType(typeof(RetificacaoEmCursoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [EmiteETag]
    public async Task<IActionResult> ObterRetificacaoEmCurso(Guid id, CancellationToken cancellationToken)
    {
        RetificacaoEmCursoDto? rascunho = await _queryBus
            .Send(new ObterRetificacaoEmCursoQuery(id), cancellationToken)
            .ConfigureAwait(false);
        if (rascunho is null)
        {
            return NotFound();
        }

        Response.Headers.ETag = rascunho.ETag;
        return Ok(rascunho);
    }

    /// <summary>
    /// Altera o motivo da sessão editorial em curso — mutação como qualquer outra: exige a
    /// precondição e devolve o <c>ETag</c> novo.
    /// </summary>
    /// <remarks>
    /// Aqui o <c>If-Match</c> é <b>sempre</b> obrigatório, e não condicional como nos seis
    /// <c>Definir*</c>: esta rota só existe <b>para</b> a sessão. Um cliente que a chama sem
    /// precondição cometeu falha de protocolo, e recebe <b>428</b> — antes mesmo de o
    /// servidor conferir se há sessão (ADR-0110 D9, precedência "3 antes de 10").
    /// </remarks>
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosPublicar)]
    [HttpPut("{id:guid}/retificacao-em-curso")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict,
        Description = "Não há retificação em curso para ter o motivo alterado. Abrir a retificação antes. Também responde 409 quando outra requisição com a mesma Idempotency-Key ainda está em processamento.")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status428PreconditionRequired)]
    [EmiteETag]
    [PrecondicaoObrigatoria]
    public async Task<IActionResult> AlterarMotivoRetificacao(
        Guid id,
        [FromBody] AlterarMotivoRetificacaoRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!ProtocoloDeMutacao.TentarLerPrecondicao(_mapper, ifMatch, out PrecondicaoIfMatch precondicao, out IActionResult? malformada))
            return malformada!;

        Result<MutacaoAceita> resultado = await _commandBus.Send(
            new AlterarMotivoRetificacaoCommand(id, request.Motivo, precondicao), cancellationToken);
        return ProtocoloDeMutacao.ResponderMutacao(_mapper, Response, resultado);
    }

    /// <summary>
    /// <b>Descarta</b> a sessão editorial: o administrador abriu e desistiu.
    /// </summary>
    /// <remarks>
    /// Repõe a configuração ao que a versão congelada diz — <b>com prova de round-trip byte a
    /// byte</b> — e encerra a sessão, na mesma transação. Não devolve <c>ETag</c>: a sessão
    /// deixou de existir.
    /// </remarks>
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosPublicar)]
    [HttpDelete("{id:guid}/retificacao-em-curso")]
    [RequiresIdempotencyKey]
    [PrecondicaoObrigatoria]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict,
        Description = "Não há retificação em curso para descartar, ou a base dela ficou para trás da versão vigente. Reler o estado da retificação antes de repetir. Também responde 409 quando outra requisição com a mesma Idempotency-Key ainda está em processamento.")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status428PreconditionRequired)]
    public async Task<IActionResult> DescartarRetificacao(
        Guid id,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken)
    {
        if (!ProtocoloDeMutacao.TentarLerPrecondicao(_mapper, ifMatch, out PrecondicaoIfMatch precondicao, out IActionResult? malformada))
            return malformada!;

        Result resultado = await _commandBus.Send(
            new DescartarRetificacaoCommand(id, precondicao), cancellationToken);

        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// <b>Fecha</b> a sessão editorial: congela a versão N+1 <b>com a configuração editada</b>
    /// e registra o ato.
    /// </summary>
    /// <remarks>
    /// O corpo é o do atalho atômico <b>menos o motivo</b> — ele já está no rascunho, declarado
    /// na abertura. Não devolve <c>ETag</c>: a sessão deixou de existir.
    /// <para>
    /// O <c>POST /{id}/retificacoes</c> continua existindo e inalterado: ele é a retificação
    /// em <b>um ato só</b>, para quem não precisa de sessão. Com sessão aberta, ele recusa.
    /// </para>
    /// </remarks>
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosPublicar)]
    [HttpPost("{id:guid}/retificacao-em-curso/fechamento")]
    [RequiresIdempotencyKey]
    [PrecondicaoObrigatoria]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict,
        Description = "A retificação não está aberta, a base dela ficou para trás da versão vigente, ou outra publicação concorrente congelou uma versão entre a leitura e a gravação desta. Reler a retificação em curso antes de repetir. Também responde 409 quando outra requisição com a mesma Idempotency-Key ainda está em processamento.")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status428PreconditionRequired)]
    public async Task<IActionResult> FecharRetificacao(
        Guid id,
        [FromBody] FecharRetificacaoRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!ProtocoloDeMutacao.TentarLerPrecondicao(_mapper, ifMatch, out PrecondicaoIfMatch precondicao, out IActionResult? malformada))
            return malformada!;

        Result resultado = await _commandBus.Send(
            new FecharRetificacaoCommand(
                id,
                request.Numero,
                request.PeriodoInscricaoInicio,
                request.PeriodoInscricaoFim,
                request.DocumentoEditalId,
                MapeamentoDoAto.ParaDados(request.Ato),
                precondicao,
                MapeamentoDoAto.ParaAnexos(request.Anexos)),
            cancellationToken);

        if (resultado.IsSuccess)
        {
            return NoContent();
        }

        IActionResult actionResult = resultado.ToActionResult(_mapper);
        return await EnriquecimentoDaPublicacao.EnriquecerComObrigatoriedadesReprovadasAsync(
            _queryBus, resultado, actionResult, id, request.PeriodoInscricaoInicio, cancellationToken).ConfigureAwait(false);
    }
}
