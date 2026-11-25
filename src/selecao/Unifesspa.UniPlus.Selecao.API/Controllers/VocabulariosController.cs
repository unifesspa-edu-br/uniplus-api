namespace Unifesspa.UniPlus.Selecao.API.Controllers;

using System.Collections.Generic;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.Vocabularios;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Vocabulários fechados que a configuração de um Processo Seletivo referencia por código:
/// os fundamentos de isenção de taxa (UNI-REQ-0101), os campos publicáveis na divulgação
/// de resultado (UNI-REQ-0050) e as opções do recurso: a unidade do prazo, o efeito
/// suspensivo e a guarda da vaga.
/// </summary>
/// <remarks>
/// <para>
/// Existem para que o cliente descubra os códigos em runtime. Sem eles, cada tela que monta
/// uma dessas configurações precisa manter sua própria cópia dos tokens, e a cópia
/// envelhece sem avisar — o drift só aparece como requisição recusada, do lado de quem
/// preencheu o formulário corretamente.
/// </para>
/// <para>
/// <b>Somente leitura, e não por falta de tempo.</b> Os dois conjuntos são governados por
/// código: mudam por versão da API, com a mudança de regra que os acompanha, e não por
/// cadastro administrativo. Um CRUD aqui permitiria acrescentar um fundamento que nenhuma
/// verificação sabe conferir, ou um campo de candidato que nenhuma regra de minimização
/// autorizou.
/// </para>
/// <para>
/// A leitura é anônima, como a dos demais catálogos institucionais do módulo: o conteúdo é
/// o vocabulário que o edital publica, sem nenhum dado de candidato.
/// </para>
/// </remarks>
[ApiController]
public sealed class VocabulariosController : ControllerBase
{
    private readonly IQueryBus _queryBus;

    public VocabulariosController(IQueryBus queryBus)
    {
        _queryBus = queryBus;
    }

    /// <summary>
    /// Lista os fundamentos de isenção referenciáveis por um processo que cobra taxa, na
    /// ordem canônica. Referenciar um fundamento não decide origem do fato, forma de
    /// comprovação nem quem analisa o pedido — isso pertence à verificação de cada
    /// fundamento, ainda fora desta configuração.
    /// </summary>
    [HttpGet("fundamentos-isencao")]
    [EndpointName("ListarFundamentosDeIsencao")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "fundamento-isencao", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<FundamentoIsencaoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ListarFundamentosIsencao(CancellationToken cancellationToken)
    {
        IReadOnlyList<FundamentoIsencaoDto> fundamentos = await _queryBus
            .Send(new ListarFundamentosIsencaoQuery(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(fundamentos);
    }

    /// <summary>
    /// Lista os campos permitidos na divulgação pública de resultado, com o piso que nenhuma
    /// configuração remove e o campo cuja publicação exige justificativa.
    /// </summary>
    [HttpGet("campos-divulgacao")]
    [EndpointName("ListarCamposDeDivulgacao")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "campo-divulgacao", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<CampoDivulgacaoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ListarCamposDivulgacao(CancellationToken cancellationToken)
    {
        IReadOnlyList<CampoDivulgacaoDto> campos = await _queryBus
            .Send(new ListarCamposDivulgacaoQuery(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(campos);
    }

    /// <summary>
    /// Lista as unidades em que o edital declara o prazo do recurso: horas e dias úteis. A de
    /// dias corridos não é oferecida, porque o prazo que corre em dia sem expediente encolhe
    /// sem que a pessoa possa agir.
    /// </summary>
    [HttpGet("unidades-do-prazo-de-recurso")]
    [EndpointName("ListarUnidadesDoPrazoDeRecurso")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "unidade-do-prazo-de-recurso", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<CodigoComRotulo<UnidadePrazo>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ListarUnidadesDoPrazoDeRecurso(CancellationToken cancellationToken)
    {
        IReadOnlyList<CodigoComRotulo<UnidadePrazo>> unidades = await _queryBus
            .Send(new ListarUnidadesDoPrazoDeRecursoQuery(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(unidades);
    }

    /// <summary>
    /// Lista as opções de efeito suspensivo do recurso da 1ª instância, cada uma com a
    /// explicação do que ela significa para o processo.
    /// </summary>
    [HttpGet("efeitos-suspensivos")]
    [EndpointName("ListarEfeitosSuspensivos")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "efeito-suspensivo", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<CodigoComDescricao<EfeitoSuspensivo>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ListarEfeitosSuspensivos(CancellationToken cancellationToken)
    {
        IReadOnlyList<CodigoComDescricao<EfeitoSuspensivo>> efeitos = await _queryBus
            .Send(new ListarEfeitosSuspensivosQuery(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(efeitos);
    }

    /// <summary>
    /// Lista as opções de guarda da vaga enquanto o recurso corre, cada uma com a explicação
    /// do que ela significa para quem recorre.
    /// </summary>
    [HttpGet("guardas-da-vaga")]
    [EndpointName("ListarGuardasDaVaga")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "guarda-da-vaga", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<CodigoComDescricao<GuardaDaVaga>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ListarGuardasDaVaga(CancellationToken cancellationToken)
    {
        IReadOnlyList<CodigoComDescricao<GuardaDaVaga>> guardas = await _queryBus
            .Send(new ListarGuardasDaVagaQuery(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(guardas);
    }

    /// <summary>Lista os modos de deliberação com código e rótulo.</summary>
    [HttpGet("vocabularios/modos-deliberacao")]
    [EndpointName("ListarModosDeDeliberacao")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "modo-deliberacao", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<CodigoComRotulo<ModoDeDeliberacao>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ListarModosDeDeliberacao(CancellationToken cancellationToken)
    {
        IReadOnlyList<CodigoComRotulo<ModoDeDeliberacao>> opcoes = await _queryBus
            .Send(new ListarModosDeDeliberacaoQuery(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(opcoes);
    }

    /// <summary>Lista os critérios de votação com código e rótulo.</summary>
    [HttpGet("vocabularios/criterios-votacao")]
    [EndpointName("ListarCriteriosDeVotacao")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "criterio-votacao", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<CodigoComRotulo<CriterioDeVotacao>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ListarCriteriosDeVotacao(CancellationToken cancellationToken)
    {
        IReadOnlyList<CodigoComRotulo<CriterioDeVotacao>> opcoes = await _queryBus
            .Send(new ListarCriteriosDeVotacaoQuery(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(opcoes);
    }

    /// <summary>Lista os desempates da votação com código e rótulo.</summary>
    [HttpGet("vocabularios/desempates-votacao")]
    [EndpointName("ListarDesempatesDaVotacao")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "desempate-votacao", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<CodigoComRotulo<DesempateDaVotacao>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ListarDesempatesDaVotacao(CancellationToken cancellationToken)
    {
        IReadOnlyList<CodigoComRotulo<DesempateDaVotacao>> opcoes = await _queryBus
            .Send(new ListarDesempatesDaVotacaoQuery(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(opcoes);
    }

    /// <summary>Lista os encerramentos da votação com código e rótulo.</summary>
    [HttpGet("vocabularios/encerramentos-votacao")]
    [EndpointName("ListarEncerramentosDaVotacao")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "encerramento-votacao", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<CodigoComRotulo<EncerramentoDaVotacao>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ListarEncerramentosDaVotacao(CancellationToken cancellationToken)
    {
        IReadOnlyList<CodigoComRotulo<EncerramentoDaVotacao>> opcoes = await _queryBus
            .Send(new ListarEncerramentosDaVotacaoQuery(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(opcoes);
    }

    /// <summary>Lista os resultados sem unanimidade com código e rótulo.</summary>
    [HttpGet("vocabularios/resultados-sem-unanimidade")]
    [EndpointName("ListarResultadosSemUnanimidade")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "resultado-sem-unanimidade", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<CodigoComRotulo<ResultadoSemUnanimidade>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ListarResultadosSemUnanimidade(CancellationToken cancellationToken)
    {
        IReadOnlyList<CodigoComRotulo<ResultadoSemUnanimidade>> opcoes = await _queryBus
            .Send(new ListarResultadosSemUnanimidadeQuery(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(opcoes);
    }
}
