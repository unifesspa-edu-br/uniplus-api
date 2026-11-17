namespace Unifesspa.UniPlus.Selecao.API.Controllers;

using System.Diagnostics.CodeAnalysis;

using Application.Commands.MotivosDoProcesso;
using Application.Commands.ProcessosSeletivos;
using Application.DTOs;
using Application.Queries.MotivosDoProcesso;

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
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

/// <summary>
/// Os motivos de decisão que o processo escolhe do cadastro, em cada fase ou etapa (UNI-REQ-0285). A escolha é só no
/// rascunho; depois da publicação, a retificação inclui o motivo pelo documento de configuração, com justificativa e
/// antes de começar a avaliação naquele lugar, e nada do que foi publicado se retira.
/// </summary>
[ApiController]
[EmiteEsperaEsgotadaPelaTrava]
[Route("processos-seletivos")]
[Tags("ProcessoSeletivo")]
[Authorize]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public; sem isso o MVC ignora a classe e nenhum endpoint é registrado.")]
public sealed class ProcessoSeletivoMotivosController : ControllerBase
{
    private readonly ICommandBus _commandBus;
    private readonly IQueryBus _queryBus;
    private readonly IDomainErrorMapper _mapper;

    public ProcessoSeletivoMotivosController(ICommandBus commandBus, IQueryBus queryBus, IDomainErrorMapper mapper)
    {
        _commandBus = commandBus;
        _queryBus = queryBus;
        _mapper = mapper;
    }

    /// <summary>
    /// Os motivos escolhidos pelo processo em cada fase e etapa, com a justificativa interna das inclusões feitas depois
    /// da publicação — por isso a leitura exige a permissão de configurar o processo. Com <c>versao</c>, a escolha que
    /// valia naquela versão publicada do edital; sem ela, a escolha atual.
    /// </summary>
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosConfigurar)]
    [HttpGet("{id:guid}/motivos-de-decisao")]
    [VendorMediaType(Resource = "motivo-do-processo", Versions = [1])]
    [ProducesResponseType(typeof(IReadOnlyList<MotivoDoProcessoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> Listar(Guid id, [FromQuery(Name = "versao")] int? versao, CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<MotivoDoProcessoDto>> motivos = await _queryBus
            .Send(new ListarMotivosDoProcessoQuery(id, versao), cancellationToken)
            .ConfigureAwait(false);
        return motivos.IsFailure ? motivos.ToActionResult(_mapper) : Ok(motivos.Value);
    }

    /// <summary>
    /// Inclui na fase o motivo do cadastro, só em rascunho. Não confere se a fase declara o tipo de decisão do motivo: a
    /// publicação e a retificação conferem.
    /// </summary>
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosConfigurar)]
    [HttpPut("{id:guid}/fases/{faseCanonicaOrigemId:guid}/motivos-de-decisao/{motivoDeDecisaoId:guid}")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> IncluirNaFase(Guid id, Guid faseCanonicaOrigemId, Guid motivoDeDecisaoId, CancellationToken cancellationToken) =>
        IncluirAsync(id, LugarDaFase(faseCanonicaOrigemId), motivoDeDecisaoId, cancellationToken);

    /// <summary>
    /// Inclui na etapa o motivo do cadastro, só em rascunho. Não confere se a etapa declara o tipo de decisão do motivo: a
    /// publicação e a retificação conferem.
    /// </summary>
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosConfigurar)]
    [HttpPut("{id:guid}/etapas/{etapaId:guid}/motivos-de-decisao/{motivoDeDecisaoId:guid}")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> IncluirNaEtapa(Guid id, Guid etapaId, Guid motivoDeDecisaoId, CancellationToken cancellationToken) =>
        IncluirAsync(id, LugarDaEtapa(etapaId), motivoDeDecisaoId, cancellationToken);

    /// <summary>Retira da fase o motivo escolhido, só em rascunho; o publicado não sai (UNI-REQ-0124).</summary>
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosConfigurar)]
    [HttpDelete("{id:guid}/fases/{faseCanonicaOrigemId:guid}/motivos-de-decisao/{motivoDeDecisaoId:guid}")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> RetirarDaFase(Guid id, Guid faseCanonicaOrigemId, Guid motivoDeDecisaoId, CancellationToken cancellationToken) =>
        RetirarAsync(id, LugarDaFase(faseCanonicaOrigemId), motivoDeDecisaoId, cancellationToken);

    /// <summary>Retira da etapa o motivo escolhido, só em rascunho; o publicado não sai (UNI-REQ-0124).</summary>
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosConfigurar)]
    [HttpDelete("{id:guid}/etapas/{etapaId:guid}/motivos-de-decisao/{motivoDeDecisaoId:guid}")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> RetirarDaEtapa(Guid id, Guid etapaId, Guid motivoDeDecisaoId, CancellationToken cancellationToken) =>
        RetirarAsync(id, LugarDaEtapa(etapaId), motivoDeDecisaoId, cancellationToken);

    // A identidade vazia não designa fase nem etapa: o lugar não existe, e o domínio não a aceita como lugar.
    private static LugarDaDecisao? LugarDaFase(Guid faseCanonicaOrigemId) =>
        faseCanonicaOrigemId == Guid.Empty ? null : LugarDaDecisao.DaFase(faseCanonicaOrigemId);

    private static LugarDaDecisao? LugarDaEtapa(Guid etapaId) =>
        etapaId == Guid.Empty ? null : LugarDaDecisao.DaEtapa(etapaId);

    private async Task<IActionResult> IncluirAsync(Guid id, LugarDaDecisao? lugar, Guid motivoDeDecisaoId, CancellationToken cancellationToken)
    {
        if (lugar is null)
            return Result.Failure(EscolhaDosMotivos.LugarNaoEncontrado()).ToActionResult(_mapper);

        Result resultado = await _commandBus.Send(new IncluirMotivoNoProcessoCommand(id, lugar, motivoDeDecisaoId), cancellationToken);
        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }

    private async Task<IActionResult> RetirarAsync(Guid id, LugarDaDecisao? lugar, Guid motivoDeDecisaoId, CancellationToken cancellationToken)
    {
        if (lugar is null)
            return Result.Failure(EscolhaDosMotivos.LugarNaoEncontrado()).ToActionResult(_mapper);

        Result resultado = await _commandBus.Send(new RetirarMotivoDoProcessoCommand(id, lugar, motivoDeDecisaoId), cancellationToken);
        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }
}
