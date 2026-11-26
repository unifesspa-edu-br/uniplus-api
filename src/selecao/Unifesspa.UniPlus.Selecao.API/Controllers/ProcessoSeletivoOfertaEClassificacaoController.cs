namespace Unifesspa.UniPlus.Selecao.API.Controllers;

using System.Diagnostics.CodeAnalysis;

using Application.Commands.ProcessosSeletivos;

using Contracts.Requests;

using Http;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Infrastructure.Core.OpenApi;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.API.Authorization;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Configuração dos critérios de desempate, do FADA, das opções de curso, da taxa de inscrição, da divulgação, da cascata de remanejamento e da classificação do processo seletivo.
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
public sealed class ProcessoSeletivoOfertaEClassificacaoController : ControllerBase
{
    private readonly ICommandBus _commandBus;
    private readonly IDomainErrorMapper _mapper;

    public ProcessoSeletivoOfertaEClassificacaoController(
        ICommandBus commandBus,
        IDomainErrorMapper mapper)
    {
        _commandBus = commandBus;
        _mapper = mapper;
    }

    /// <summary>
    /// Substitui integralmente os critérios de desempate do processo. Lista vazia remove todos os critérios: o rascunho aceita, mas a publicação de
    /// processo com inscrição própria exige ao menos um (resultado importado dispensa).
    /// </summary>
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosConfigurar)]
    [HttpPut("{id:guid}/criterios-desempate")]
    [EndpointName("DefinirCriteriosDeDesempate")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> DefinirCriteriosDesempate(
        Guid id,
        [FromBody] IReadOnlyList<CriterioDesempateInput> criterios,
        CancellationToken cancellationToken)
    {
        Result resultado = await _commandBus.Send(
            new DefinirCriteriosDesempateCommand(id, criterios), cancellationToken);
        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Declara se o processo aplica o Fator de Atenuação das Desigualdades de Acesso (FADA, UNI-REQ-0015). <c>Aplica</c> falso
    /// dispensa a configuração; verdadeiro exige <c>Regra</c> e os demais campos do FADA.
    /// A declaração é obrigatória para publicar.
    /// </summary>
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosConfigurar)]
    [HttpPut("{id:guid}/fada")]
    [EndpointName("DefinirFadaDoProcesso")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> DefinirFada(
        Guid id,
        [FromBody] DefinirFadaRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result resultado = await _commandBus.Send(
            new DefinirFadaCommand(id, request.Aplica, request.Regra, request.Fator, request.Teto, request.FatoElegibilidadeCodigo, request.BaseLegal),
            cancellationToken);
        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }

    /// <summary>Substitui a declaração da prova presencial e seus municípios de prova.</summary>
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosConfigurar)]
    [HttpPut("{id:guid}/prova-presencial")]
    [EndpointName("DefinirProvaPresencial")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> DefinirProvaPresencial(Guid id, [FromBody] DefinirProvaPresencialRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        Result resultado = await _commandBus.Send(new DefinirProvaPresencialCommand(id, request.TemProvaPresencial, request.Municipios), cancellationToken);
        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Substitui o cadastro de reserva por chamada inteiro: cada item traz o número da chamada, a partir de 1 e declarado uma só vez, e quantas pessoas da
    /// reserva a chamada convoca por vaga, que pode ser zero. A chamada com pessoas da reserva escolhe a participação da reserva (a reserva só antecipa a
    /// habilitação ou assume na mesma chamada), sem valor padrão; a chamada com zero não tem participação. A chamada que não consta na lista não tem
    /// reserva, e a lista vazia deixa o processo sem reserva.
    /// </summary>
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosConfigurar)]
    [HttpPut("{id:guid}/cadastro-reserva")]
    [EndpointName("DefinirCadastroDeReserva")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> DefinirCadastroDeReserva(Guid id, [FromBody] DefinirCadastroDeReservaRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        Result resultado = await _commandBus.Send(new DefinirCadastroDeReservaCommand(id, request.Chamadas), cancellationToken);
        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Declara como o edital prevê a manifestação de interesse depois da 1ª chamada: <c>UMA_VEZ</c> ou <c>CONFIRMA_A_CADA_CHAMADA</c>, que prevê a
    /// manifestação e a exige, a segunda com confirmação a cada chamada. O código nulo diz que o edital não prevê a manifestação, e a declaração não é
    /// obrigatória para publicar. O prazo da manifestação é da publicação de cada chamada, não desta declaração.
    /// </summary>
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosConfigurar)]
    [HttpPut("{id:guid}/manifestacao-interesse")]
    [EndpointName("DefinirManifestacaoDeInteresse")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> DefinirManifestacaoDeInteresse(Guid id, [FromBody] DefinirManifestacaoDeInteresseRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        Result resultado = await _commandBus.Send(new DefinirManifestacaoDeInteresseCommand(id, request.ManifestacaoDeInteresse), cancellationToken);
        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Declara quantos cursos o candidato escolhe na inscrição (1 ou 2). A declaração é obrigatória
    /// para publicar, e o número não pode superar o de ofertas da distribuição de vagas.
    /// </summary>
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosConfigurar)]
    [HttpPut("{id:guid}/opcoes-de-curso")]
    [EndpointName("DefinirOpcoesDeCurso")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> DefinirOpcoesDeCurso(
        Guid id,
        [FromBody] DefinirOpcoesDeCursoRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result resultado = await _commandBus.Send(
            new DefinirOpcoesDeCursoCommand(id, request.Numero),
            cancellationToken);
        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Define (ou remove) a taxa de inscrição e os fundamentos de isenção do processo. <c>Cobra</c> nulo remove a declaração — o processo volta a "ainda não
    /// declarado", que BLOQUEIA a publicação.
    /// </summary>
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosConfigurar)]
    [HttpPut("{id:guid}/taxa-inscricao")]
    [EndpointName("DefinirTaxaDeInscricao")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> DefinirTaxaInscricao(
        Guid id,
        [FromBody] DefinirTaxaInscricaoRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result resultado = await _commandBus.Send(
            new DefinirTaxaInscricaoCommand(id, request.Cobra, request.Valor, request.Fundamentos),
            cancellationToken);
        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Define (ou remove) a divulgação pública do processo (UNI-REQ-0050).
    /// <c>CamposPublicos</c> nulo remove a configuração explícita — o processo volta ao
    /// default minimizado (só o número de inscrição).
    /// </summary>
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosConfigurar)]
    [HttpPut("{id:guid}/divulgacao")]
    [EndpointName("DefinirCamposPublicosDaDivulgacao")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> DefinirConfiguracaoDivulgacao(
        Guid id,
        [FromBody] DefinirConfiguracaoDivulgacaoRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result resultado = await _commandBus.Send(
            new DefinirConfiguracaoDivulgacaoCommand(id, request.CamposPublicos, request.Justificativa),
            cancellationToken);
        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Define (ou remove) a cascata de remanejamento do processo
    /// (cascata de remanejamento de cotas, UNI-REQ-0056). <c>RegraCodigo</c> nulo remove a
    /// cascata — a ausência já é o toggle "sem cascata".
    /// </summary>
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosConfigurar)]
    [HttpPut("{id:guid}/cascata-remanejamento")]
    [EndpointName("DefinirCascataDeRemanejamento")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> DefinirCascataRemanejamento(
        Guid id,
        [FromBody] DefinirCascataRemanejamentoRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result resultado = await _commandBus.Send(
            new DefinirCascataRemanejamentoCommand(id, request.RegraCodigo, request.RegraVersao, request.FallbackCodigo, request.Destinos),
            cancellationToken);
        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Substitui integralmente a configuração de classificação do processo
    /// — o 15º bloco canônico, que compõe
    /// por referência a fórmula da nota, a precisão, a lista de eliminação e
    /// a ordem de alocação. O FADA e o desempate não são parâmetros aqui.
    /// </summary>
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosConfigurar)]
    [HttpPut("{id:guid}/classificacao")]
    [EndpointName("DefinirClassificacao")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> DefinirClassificacao(
        Guid id,
        [FromBody] DefinirClassificacaoRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result resultado = await _commandBus.Send(request.ParaComando(id), cancellationToken);
        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }
}
