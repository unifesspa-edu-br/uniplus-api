namespace Unifesspa.UniPlus.Selecao.API.Controllers;

using System.Diagnostics.CodeAnalysis;

using Application.Commands.AtosDoProcesso;
using Application.DTOs;
using Application.Queries.AtosDoProcesso;

using Contracts.Requests;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Atos do processo seletivo na visão administrativa: o edital, as retificações e os demais atos
/// publicados, com a situação do registro e os documentos de cada um.
/// </summary>
[ApiController]
[Route("processos-seletivos/{processoSeletivoId:guid}/atos")]
// Mesma justificativa de ProcessoSeletivoController: fluxo administrativo de publicação, sem
// fallback policy — restrito a plataforma-admin.
[Authorize(Roles = "plataforma-admin")]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public; sem isso o MVC ignora a classe e nenhum endpoint é registrado.")]
public sealed class AtosDoProcessoController : ControllerBase
{
    private readonly ICommandBus _commandBus;
    private readonly IQueryBus _queryBus;
    private readonly IDomainErrorMapper _mapper;

    public AtosDoProcessoController(ICommandBus commandBus, IQueryBus queryBus, IDomainErrorMapper mapper)
    {
        _commandBus = commandBus;
        _queryBus = queryBus;
        _mapper = mapper;
    }

    /// <summary>
    /// Publica um ato avulso do processo — aviso, comunicado, errata e semelhantes —, com o documento
    /// principal em PDF e os anexos já enviados, e, quando corrige um ato publicado, a indicação dele e o
    /// motivo (ADR-0103). Tudo o que o registro central recusaria depois é conferido antes, com 422: a
    /// recusa tardia iria para a fila de mensagens que falharam, onde ninguém a recebe.
    /// </summary>
    /// <remarks>
    /// Responde 202: o registro no Publicações e a cópia ao acervo público acontecem depois, por mensagem
    /// durável. O ato aparece como pendente na consulta dos atos do processo até o registro se confirmar.
    /// Convocação e chamada não são documento avulso: pertencem ao Ingresso.
    /// </remarks>
    [HttpPost]
    [RequiresIdempotencyKey]
    [ProducesResponseType(typeof(AtoPublicadoDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict,
        Description = "Outra requisição com a mesma Idempotency-Key ainda está em processamento.")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Publicar(
        Guid processoSeletivoId,
        [FromBody] PublicarAtoAvulsoDoProcessoRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<AtoPublicadoDto> resultado = await _commandBus
            .Send(
                new PublicarAtoAvulsoDoProcessoCommand(
                    processoSeletivoId,
                    request.TipoAtoCodigo,
                    request.Numero,
                    request.DataPublicacao,
                    request.Assinante,
                    request.DocumentoPrincipalId,
                    AnexoDoAtoRequest.ParaComando(request.Anexos),
                    request.AtoCorrigidoId,
                    request.MotivoDaCorrecao),
                cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? StatusCode(StatusCodes.Status202Accepted, resultado.Value) : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Lista os atos do processo, do mais recente para o mais antigo. O ato pendente há mais tempo
    /// que o limiar configurado vem com <c>pendenciaAcimaDoLimiar</c>: o Publicações não responde a
    /// recusa do registro, então é por aqui que a pendência chega a quem a resolve.
    /// </summary>
    [HttpGet]
    [VendorMediaType(Resource = "ato-do-processo", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<AtoDoProcessoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> Listar(Guid processoSeletivoId, CancellationToken cancellationToken)
    {
        Result<ListarAtosDoProcessoResult> resultado = await _queryBus
            .Send(new ListarAtosDoProcessoQuery(processoSeletivoId), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? Ok(resultado.Value!.Items) : resultado.ToActionResult(_mapper);
    }
}
