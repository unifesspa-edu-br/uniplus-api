namespace Unifesspa.UniPlus.Selecao.API.Controllers;

using System.Diagnostics.CodeAnalysis;

using Application.Commands.ProcessosSeletivos;
using Application.Queries.ProcessosSeletivos;

using Contracts.Requests;

using Http;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Hateoas;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.API.Authorization;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;

/// <summary>
/// Publicação do processo seletivo: congela a primeira versão da configuração e registra o ato em Publicações.
/// </summary>
[ApiController]
[Route("processos-seletivos")]
[Tags("ProcessoSeletivo")]
[Authorize]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public; sem isso o MVC ignora a classe e nenhum endpoint é registrado.")]
public sealed class ProcessoSeletivoPublicacaoController : ControllerBase
{
    private readonly ICommandBus _commandBus;
    private readonly IQueryBus _queryBus;
    private readonly IDomainErrorMapper _mapper;

    public ProcessoSeletivoPublicacaoController(
        ICommandBus commandBus,
        IQueryBus queryBus,
        IDomainErrorMapper mapper)
    {
        _commandBus = commandBus;
        _queryBus = queryBus;
        _mapper = mapper;
    }

    /// <summary>
    /// Publica o processo (congelamento dos parâmetros na publicação, UNI-REQ-0019): valida a conformidade, congela a versão 1 da
    /// configuração (append-only) e transita o status para Publicado, tudo na mesma
    /// transação — junto da requisição durável que registra o ato em Publicações
    /// (ADR-0108). Toda recusa 422 é reavaliada contra o checklist estrutural
    /// (issue #1096): havendo item vermelho no estado atual, o corpo carrega
    /// <c>Extensions["pendencias"]</c>, qualquer que seja o código do erro que
    /// efetivamente recusou.
    /// <para>
    /// Devolve <b>409</b> quando outra publicação congelou uma versão entre a leitura da
    /// anterior e a gravação desta: a requisição foi montada contra um estado que deixou de
    /// ser o atual, e o corpo não tem defeito. É o caso de reler e resubmeter.
    /// </para>
    /// <para>
    /// O bloco <c>ato</c> é o MESMO que a retificação recebe: o tipo do ato vem
    /// declarado pelo operador e é conferido contra o catálogo de Publicações — nunca
    /// inferido do contexto. Retificar não é um tipo de ato, é uma relação entre atos
    /// (ADR-0103).
    /// </para>
    /// </summary>
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosPublicar)]
    [HttpPost("{id:guid}/publicacao")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict,
        Description = "Outra publicação concorrente congelou uma versão da configuração entre a leitura e a gravação desta. Reler o processo e resubmeter. Também responde 409 quando outra requisição com a mesma Idempotency-Key ainda está em processamento.")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Publicar(
        Guid id,
        [FromBody] PublicarProcessoSeletivoRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result resultado = await _commandBus.Send(
            new PublicarProcessoSeletivoCommand(
                id, request.Numero, request.PeriodoInscricaoInicio, request.PeriodoInscricaoFim, request.DocumentoEditalId,
                MapeamentoDoAto.ParaDados(request.Ato), MapeamentoDoAto.ParaAnexos(request.Anexos)),
            cancellationToken);
        if (resultado.IsSuccess)
        {
            return NoContent();
        }

        IActionResult actionResult = resultado.ToActionResult(_mapper);
        actionResult = await EnriquecimentoDaPublicacao
            .EnriquecerComPendenciasEstruturaisAsync(_queryBus, actionResult, id, cancellationToken)
            .ConfigureAwait(false);
        return await EnriquecimentoDaPublicacao.EnriquecerComObrigatoriedadesReprovadasAsync(
            _queryBus, resultado, actionResult, id, request.PeriodoInscricaoInicio, cancellationToken).ConfigureAwait(false);
    }
}
