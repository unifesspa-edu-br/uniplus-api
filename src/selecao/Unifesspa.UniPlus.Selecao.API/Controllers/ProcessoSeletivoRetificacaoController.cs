namespace Unifesspa.UniPlus.Selecao.API.Controllers;

using System.Diagnostics.CodeAnalysis;

using Application.DTOs;
using Application.Queries.ProcessosSeletivos;

using Contracts.Requests;

using Http;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Infrastructure.Core.OpenApi;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.API.Authorization;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;

/// <summary>
/// Retificação do processo já publicado, em um ato só.
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
    /// </summary>
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosPublicar)]
    [HttpPost("{id:guid}/retificacoes")]
    [EndpointName("RetificarProcessoSeletivo")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict,
        Description = "Responde 409 quando outra publicação ou retificação concorrente congelou uma versão da configuração entre a leitura e a gravação desta (reler o processo e enviar novamente), quando o identificador legível já é de outro certame (uniplus.selecao.processo_seletivo.identificador_legivel_em_uso), o objeto já tem ato vivo do tipo (uniplus.publicacoes.ato_normativo.objeto_ja_tem_ato_vivo_do_tipo), o ato que se corrige já foi retificado (uniplus.publicacoes.ato_normativo.ato_ja_retificado) e quando outra requisição com a mesma Idempotency-Key ainda está em processamento.")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Retificar(
        Guid id,
        [FromBody] RetificarProcessoSeletivoRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result resultado = await _commandBus
            .Send(request.ParaComando(id), cancellationToken)
            .ConfigureAwait(false);

        if (resultado.IsSuccess)
        {
            return NoContent();
        }

        IActionResult actionResult = resultado.ToActionResult(_mapper);
        return await EnriquecimentoDaPublicacao.EnriquecerComObrigatoriedadesReprovadasAsync(
            _queryBus, resultado, actionResult, id, request.PeriodoInscricaoInicio, cancellationToken).ConfigureAwait(false);
    }
}
