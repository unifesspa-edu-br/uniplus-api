namespace Unifesspa.UniPlus.Selecao.API.Controllers;

using System.Diagnostics.CodeAnalysis;

using Application.DTOs;
using Application.Queries.ProcessosSeletivos;

using Contracts.Responses;

using Http;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Infrastructure.Core.OpenApi;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.API.Authorization;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;

/// <summary>
/// Conformidade estrutural e legal do processo seletivo, consultada antes de publicar ou retificar.
/// </summary>
[ApiController]
[Route("processos-seletivos")]
[Tags("ProcessoSeletivo")]
[Authorize]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public; sem isso o MVC ignora a classe e nenhum endpoint é registrado.")]
public sealed class ProcessoSeletivoVerificacaoController : ControllerBase
{
    private readonly IQueryBus _queryBus;
    private readonly IDomainErrorMapper _mapper;

    public ProcessoSeletivoVerificacaoController(
        IQueryBus queryBus,
        IDomainErrorMapper mapper)
    {
        _queryBus = queryBus;
        _mapper = mapper;
    }

    /// <summary>
    /// Consulta a conformidade ESTRUTURAL do processo (CA-07, issue #1092): checklist
    /// bicondicional com os seis gates estruturais de <c>Publicar</c>/<c>Retificar</c> — todo
    /// item <c>Ok</c> se e somente se nenhum dos seis gates tem pendência. Não altera o
    /// processo.
    /// </summary>
    /// <remarks>
    /// <b>Não é "publicável".</b> Mesmo com todos os itens <c>Ok</c>, a publicação ainda pode
    /// recusar por conformidade LEGAL (<see cref="ObterConformidadeLegal"/>), documento
    /// confirmado, tipo de ato e outras leituras request-specific que só o comando de publicação
    /// avalia — este checklist cobre só a publicabilidade estrutural do agregado.
    /// <para>
    /// <b>Avisos.</b> A resposta traz também <c>avisos</c>, lista própria fora de <c>itens</c>: o
    /// que o administrador deve confirmar e que nunca impede a publicação (por exemplo, o mesmo
    /// tipo de ato declarado como definitivo em mais de uma fase ou etapa). Só esta consulta os
    /// calcula; os gates de publicação e retificação não os conhecem, e a bicondicional dos
    /// itens com os gates não muda.
    /// </para>
    /// </remarks>
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosConfigurar)]
    [HttpGet("{id:guid}/conformidade")]
    [VendorMediaType(Resource = "conformidade-processo-seletivo", Versions = [1])]
    [ProducesResponseType(typeof(ConformidadeProcessoSeletivoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ObterConformidade(Guid id, CancellationToken cancellationToken)
    {
        ConformidadeProcessoSeletivoDto? conformidade = await _queryBus
            .Send(new ObterConformidadeProcessoSeletivoQuery(id), cancellationToken)
            .ConfigureAwait(false);
        if (conformidade is null)
        {
            return NotFound();
        }

        return Ok(conformidade);
    }

    /// <summary>
    /// Consulta a conformidade LEGAL do processo (Story #853, CA-16) contra o catálogo
    /// <c>ObrigatoriedadeLegal</c> vigente na <paramref name="dataReferencia"/> informada —
    /// recurso distinto de <see cref="ObterConformidade"/> (checklist estrutural), com a
    /// MESMA fonte que o gate de <c>Publicar</c>/<c>Retificar</c>/<c>FecharRetificacao</c>
    /// usa: a regra que aparece reprovada aqui é a mesma que bloqueia a transição.
    /// <para>
    /// 404 só quando o processo não existe. Quando ele existe mas o rascunho ainda não tem de onde
    /// derivar a data de referência, a resposta é 422 com o código que o gate usaria — sem fase de
    /// coleta, <c>PeriodoInscricaoObrigatorioSemFaseDeColeta</c>; com fase de coleta sem janela,
    /// <c>FaseQueColetaInscricaoSemJanela</c>. Devolver 404 aqui dizia que o processo não existe, e
    /// o preflight da tela não tinha como distinguir a pendência de uma falha de rede (issue #1456).
    /// </para>
    /// </summary>
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosConfigurar)]
    [HttpGet("{id:guid}/conformidade-legal")]
    [VendorMediaType(Resource = "conformidade-legal-processo-seletivo", Versions = [1])]
    [ProducesResponseType(typeof(ConformidadeLegalProcessoSeletivoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ObterConformidadeLegal(
        Guid id,
        // Anulável (e não BindRequired): omitir significa "use a data que o gate usaria", que o
        // handler deriva. O tipo separa "omitida" de DateOnly.MinValue, que filtraria toda regra
        // por vigência e se leria como conformidade aprovada (issue #1350).
        [FromQuery] DateOnly? dataReferencia,
        CancellationToken cancellationToken)
    {
        Result<ConformidadeLegalProcessoSeletivoDto> conformidade = await _queryBus
            .Send(new ObterConformidadeLegalProcessoSeletivoQuery(id, dataReferencia), cancellationToken)
            .ConfigureAwait(false);

        return conformidade.IsFailure
            ? conformidade.ToActionResult(_mapper)
            : Ok(conformidade.Value!);
    }
}
