namespace Unifesspa.UniPlus.Selecao.API.Controllers;

using System.Diagnostics.CodeAnalysis;

using Application.Commands.ProcessosSeletivos;
using Application.DTOs;
using Application.Queries.ProcessosSeletivos;

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
/// Rascunho da publicação do operador e leitura do snapshot congelado vigente do processo seletivo.
/// </summary>
[ApiController]
[Route("processos-seletivos")]
[Tags("ProcessoSeletivo")]
[Authorize]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public; sem isso o MVC ignora a classe e nenhum endpoint é registrado.")]
public sealed class ProcessoSeletivoVersaoController : ControllerBase
{
    private readonly ICommandBus _commandBus;
    private readonly IQueryBus _queryBus;
    private readonly IDomainErrorMapper _mapper;

    public ProcessoSeletivoVersaoController(
        ICommandBus commandBus,
        IQueryBus queryBus,
        IDomainErrorMapper mapper)
    {
        _commandBus = commandBus;
        _queryBus = queryBus;
        _mapper = mapper;
    }

    /// <summary>
    /// Devolve o rascunho da publicação do operador corrente — o bloco do ato que ele já
    /// transcreveu e ainda não publicou.
    /// </summary>
    /// <remarks>
    /// 404 quando não há rascunho, inclusive quando o que havia já venceu. É a leitura que
    /// cobra o prazo: sem tarefa recorrente varrendo a tabela, quem lê é quem descarta.
    /// </remarks>
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosConfigurar)]
    [HttpGet("{id:guid}/rascunho-da-publicacao")]
    [VendorMediaType(Resource = "rascunho-da-publicacao", Versions = [1])]
    [ProducesResponseType(typeof(RascunhoDaPublicacaoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ObterRascunhoDaPublicacao(Guid id, CancellationToken cancellationToken)
    {
        RascunhoDaPublicacaoDto? rascunho = await _queryBus.Send(
            new ObterRascunhoDaPublicacaoQuery(id), cancellationToken);
        return rascunho is null ? NotFound() : Ok(rascunho);
    }

    /// <summary>
    /// Guarda o rascunho da publicação do operador corrente, substituindo por inteiro o que
    /// houvesse antes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Sem <c>If-Match</c>, e isso não é exceção ao padrão das demais rotas de escrita.</b> A
    /// precondição que elas exigem é da <b>sessão editorial</b> de retificação, e vale porque
    /// elas alteram a configuração congelada de um certame publicado. O rascunho não é
    /// configuração: não entra no envelope, é privado de quem o escreve e existe justamente
    /// enquanto o certame é preparado. Exigir dele o ETag da sessão acoplaria um bloco de notas
    /// à concorrência editorial do certame e, durante uma retificação, impediria de salvá-lo.
    /// </para>
    /// <para>
    /// O corpo não é validado campo a campo de propósito: rascunho de formulário pela metade é
    /// o caso de uso. A validação forte continua no ato de publicar.
    /// </para>
    /// </remarks>
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosPublicar)]
    [HttpPut("{id:guid}/rascunho-da-publicacao")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SalvarRascunhoDaPublicacao(
        Guid id,
        [FromBody] SalvarRascunhoDaPublicacaoRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result resultado = await _commandBus.Send(
            new SalvarRascunhoDaPublicacaoCommand(id, request.Versao, request.Conteudo), cancellationToken);
        return resultado.IsFailure ? resultado.ToActionResult(_mapper) : NoContent();
    }

    /// <summary>
    /// Joga fora o rascunho da publicação do operador corrente.
    /// </summary>
    /// <remarks>
    /// Idempotente: descartar o que não existe é 204, porque o pedido era que não houvesse
    /// rascunho — e não há.
    /// </remarks>
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosPublicar)]
    [HttpDelete("{id:guid}/rascunho-da-publicacao")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> DescartarRascunhoDaPublicacao(Guid id, CancellationToken cancellationToken)
    {
        Result resultado = await _commandBus.Send(
            new DescartarRascunhoDaPublicacaoCommand(id), cancellationToken);
        return resultado.IsFailure ? resultado.ToActionResult(_mapper) : NoContent();
    }

    /// <summary>
    /// Resolve o snapshot congelado vigente do processo num instante (congelamento dos parâmetros na publicação, UNI-REQ-0019,
    /// ADR-0075/0076/0104): a VERSÃO da configuração de maior
    /// <c>vigente_a_partir_de</c> ≤ o instante, desempatada pelo número da
    /// versão. Quem ordena é o relógio do sistema, não a data que o documento
    /// declara. Quando <paramref name="instante"/> é omitido, usa o relógio do
    /// servidor (ADR-0068). É o contrato de LEITURA que o runtime e os
    /// incrementos downstream consomem — a configuração congelada, não a viva.
    /// 422 (<c>Snapshot.VigenteAusente</c>) quando não há versão vigente ≤ o
    /// instante; 404 quando o processo não existe — nunca retorno silencioso.
    /// </summary>
    [AllowAnonymous]
    [ExigeLeituraDoProcesso]
    [HttpGet("{id:guid}/snapshot-vigente")]
    [VendorMediaType(Resource = "snapshot-vigente-processo-seletivo", Versions = [1])]
    [ProducesResponseType(typeof(SnapshotVigenteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ObterSnapshotVigente(
        Guid id,
        [FromQuery] DateTimeOffset? instante,
        CancellationToken cancellationToken)
    {
        Result<SnapshotVigenteDto> resultado = await _queryBus
            .Send(new LerSnapshotVigenteQuery(id, instante), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(_mapper);
    }
}
