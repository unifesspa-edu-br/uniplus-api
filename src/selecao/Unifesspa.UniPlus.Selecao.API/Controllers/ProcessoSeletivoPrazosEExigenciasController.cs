namespace Unifesspa.UniPlus.Selecao.API.Controllers;

using System.Diagnostics.CodeAnalysis;

using Application.Commands.ProcessosSeletivos;

using Contracts.Requests;

using Http;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Infrastructure.Core.OpenApi;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.API.Authorization;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Configuração do cronograma de fases, dos documentos exigidos, do identificador legível, da localidade, do algoritmo de contagem de prazo, da referência temporal dos fatos e das regras de derivação do processo seletivo.
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
public sealed class ProcessoSeletivoPrazosEExigenciasController : ControllerBase
{
    private readonly ICommandBus _commandBus;
    private readonly IDomainErrorMapper _mapper;

    public ProcessoSeletivoPrazosEExigenciasController(
        ICommandBus commandBus,
        IDomainErrorMapper mapper)
    {
        _commandBus = commandBus;
        _mapper = mapper;
    }

    /// <summary>
    /// Substitui integralmente o cronograma de fases do processo:
    /// janela, produtos publicados com o papel de cada um, fase que conclui o ciclo
    /// recursal, parecer individual, bancas requeridas, regra de recurso e regra de homologação das inscrições, por fase. A
    /// regra de homologação ausente remove a que a fase tinha, e a fase que não a declara não homologa: o cliente devolve a
    /// regra que leu. As recusas da regra se acumulam, cada uma no campo da fase, da alternativa, da condição e do campo
    /// da condição que a causou. O
    /// <c>GET</c> vem do endpoint agregado
    /// (<see cref="ProcessoSeletivoController.ObterPorId"/>), que passa a devolver <c>CronogramaFases</c> como parte
    /// do recurso — não há rota aninhada própria de leitura. A fase se identifica pela fase canônica de origem. A que sai
    /// leva junto as etapas dela e os motivos de decisão pendentes da fase e dessas etapas; motivo já publicado recusa
    /// com 422.
    /// </summary>
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosConfigurar)]
    [HttpPut("{id:guid}/cronograma-fases")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> DefinirCronogramaFases(
        Guid id,
        [FromBody] IReadOnlyList<FaseCronogramaInput> fases,
        CancellationToken cancellationToken)
    {
        Result resultado = await _commandBus.Send(
            new DefinirCronogramaFasesCommand(id, fases), cancellationToken);
        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Substitui integralmente os documentos exigidos do processo: fase,
    /// snapshot-copy do tipo de documento, aplicabilidade GERAL/CONDICIONAL,
    /// obrigatoriedade, consequência de indeferimento, grupo de satisfação, o gatilho
    /// DNF (dinâmico/multivalorado para MODALIDADE/CONDICAO_ATENDIMENTO), a base
    /// legal 1:N — validada apenas na FORMA aqui; o gate "≥1 RESOLVIDO por
    /// exigência que determina resultado" é da publicação (ADR-0074) — e a idade máxima
    /// de emissão/formato/tamanho do arquivo — aviso, não bloqueio de
    /// presença; congelados por chamada (substituição integral, não <i>merge</i> parcial).
    /// O <c>GET</c> vem do endpoint agregado (<see cref="ProcessoSeletivoController.ObterPorId"/>) — não há rota
    /// aninhada própria de leitura.
    /// </summary>
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosConfigurar)]
    [HttpPut("{id:guid}/documentos-exigidos")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> DefinirDocumentosExigidos(
        Guid id,
        [FromBody] IReadOnlyList<NoExigenciaInput> raizes,
        CancellationToken cancellationToken)
    {
        Result resultado = await _commandBus.Send(
            new DefinirDocumentosExigidosCommand(id, raizes), cancellationToken);
        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Declara ou troca o identificador legível do certame, de onde derivam o endereço
    /// público e a chave no acervo. Só em rascunho.
    /// </summary>
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosConfigurar)]
    [HttpPut("{id:guid}/identificador-legivel")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict,
        Description = "O identificador legível informado já é usado por outro processo seletivo (uniplus.selecao.processo_seletivo.identificador_legivel_em_uso). Também responde 409 quando outra requisição com a mesma Idempotency-Key ainda está em processamento.")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> DefinirIdentificadorLegivel(
        Guid id,
        [FromBody] DefinirIdentificadorLegivelRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result resultado = await _commandBus.Send(
            new DefinirIdentificadorLegivelCommand(id, request.IdentificadorLegivel),
            cancellationToken);
        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Redeclara o município cujo calendário rege a contagem dos prazos do certame.
    /// </summary>
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosConfigurar)]
    [HttpPut("{id:guid}/localidade")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> DefinirLocalidade(
        Guid id,
        [FromBody] DefinirLocalidadeRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result resultado = await _commandBus.Send(
            new DefinirLocalidadeCommand(id, request.CodigoIbge, request.Nome, request.Uf),
            cancellationToken);
        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Declara a convenção de contagem que o certame usa nos prazos que distinguem dia
    /// útil, por código e versão do rol de regras.
    /// </summary>
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosConfigurar)]
    [HttpPut("{id:guid}/algoritmo-contagem-prazo")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> DefinirAlgoritmoContagemPrazo(
        Guid id,
        [FromBody] DefinirAlgoritmoContagemPrazoRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result resultado = await _commandBus.Send(
            new DefinirAlgoritmoContagemPrazoCommand(id, request.Codigo, request.Versao),
            cancellationToken);
        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Define (ou remove) a política que ancora <c>FAIXA_ETARIA</c> na publicação. <c>Tipo</c> nulo remove a referência.
    /// </summary>
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosConfigurar)]
    [HttpPut("{id:guid}/referencia-temporal-fatos")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> DefinirReferenciaTemporalFatos(
        Guid id,
        [FromBody] DefinirReferenciaTemporalFatosRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result resultado = await _commandBus.Send(
            new DefinirReferenciaTemporalFatosCommand(id, request.Tipo, request.Data, request.FaseId),
            cancellationToken);
        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Substitui integralmente as regras que derivam os fatos derivados do processo — hoje a
    /// modalidade de concorrência. Cada configuração traz o fato derivado e a lista de
    /// regras <c>{ ordem, contribui, quando? }</c>; a regra incondicional (âncora) tem <c>quando</c>
    /// nulo. Só um fato derivado com binding de regra de derivação é alvo válido — para a
    /// modalidade, cada código contribuído tem de ser uma das modalidades ofertadas pelo processo.
    /// Editável só em rascunho; o processo publicado é recusado com <c>422</c>.
    /// </summary>
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosConfigurar)]
    [HttpPut("{id:guid}/regras-derivacao")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> DefinirRegrasDerivacao(
        Guid id,
        [FromBody] IReadOnlyList<ConfiguracaoDerivacaoInput> configuracoes,
        CancellationToken cancellationToken)
    {
        Result resultado = await _commandBus.Send(
            new DefinirRegrasDerivacaoCommand(id, configuracoes), cancellationToken);
        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }
}
