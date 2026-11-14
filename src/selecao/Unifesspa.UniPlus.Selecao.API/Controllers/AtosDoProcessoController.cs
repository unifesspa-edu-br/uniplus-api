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
using Unifesspa.UniPlus.Selecao.API.Authorization;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;

/// <summary>
/// Atos do processo seletivo na visão administrativa: o edital, as retificações e os demais atos
/// publicados, com os documentos de cada um.
/// </summary>
[ApiController]
[Route("processos-seletivos/{processoSeletivoId:guid}/atos")]
[Authorize]
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
    /// motivo (ADR-0103). O ato entra no registro central na mesma transação que grava o ato do processo, e a
    /// recusa do registro volta nomeada, sem nada gravado.
    /// </summary>
    /// <remarks>
    /// Responde 201 com o ato publicado e divulgado: a cópia dos documentos ao acervo público é o único efeito posterior,
    /// por mensagem durável. A vaga do objeto já ocupada e o ato já retificado respondem 409, com a série, o número e o ano
    /// do ato que a ocupa. Convocação e chamada não são documento avulso: pertencem ao Ingresso.
    /// </remarks>
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosPublicar, ParametroDaRota = RotaDoProcesso.ParametroAninhado)]
    [HttpPost]
    [RequiresIdempotencyKey]
    [ProducesResponseType(typeof(AtoPublicadoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict,
            Description = "O objeto já tem ato vivo do tipo (uniplus.publicacoes.ato_normativo.objeto_ja_tem_ato_vivo_do_tipo), o ato que se corrige já foi retificado (uniplus.publicacoes.ato_normativo.raiz_ja_retificada) ou outra requisição com a mesma Idempotency-Key ainda está em processamento.")]
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
                    request.DataDoAto,
                    request.Assinante,
                    request.DocumentoPrincipalId,
                    AnexoDoAtoRequest.ParaComando(request.Anexos),
                    request.AtoCorrigidoId,
                    request.MotivoDaCorrecao),
                cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess
            ? CreatedAtAction(nameof(Listar), new { processoSeletivoId }, resultado.Value)
            : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Lista os atos do processo, do mais recente para o mais antigo, com o instante em que cada um foi divulgado.
    /// </summary>
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosAcompanhar, ParametroDaRota = RotaDoProcesso.ParametroAninhado)]
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
