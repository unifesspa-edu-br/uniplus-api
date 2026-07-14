namespace Unifesspa.UniPlus.Selecao.API.Controllers;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Infrastructure.Core.Hateoas;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Infrastructure.Core.OpenApi;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Application.Commands.ProcessosSeletivos;
using Application.DTOs;
using Application.Queries.ProcessosSeletivos;
using Http;

/// <summary>
/// Configuração do Processo Seletivo (Story #758, UNI-REQ-0014/0015): o
/// administrador cria o processo em rascunho e monta a configuração sobre o
/// agregado-raiz — etapas pontuadas, oferta de atendimento especializado,
/// distribuição de vagas (Story #773), bônus regional e critérios de
/// desempate (Story #774) e classificação (Story #775, 15º bloco canônico),
/// estes por referência ao catálogo de regras tipadas versionadas
/// (<c>rol_de_regras</c>, Story #772). O <c>Edital</c> não é criado
/// diretamente — é o documento emitido pelo ato de publicação
/// (<see cref="Publicar"/>, Story #759 T4 #785).
/// </summary>
[ApiController]
[Route("api/selecao/processos-seletivos")]
// Configuração administrativa: todo o ciclo do processo em rascunho (criar,
// montar etapas/atendimento e consultar conformidade) é
// restrito a plataforma-admin — sem [Authorize] os endpoints ficariam anônimos
// (não há fallback policy). Diferente dos catálogos de reference data
// (Campi/FasesCanônicas), cujas leituras são públicas por serem dados
// publicados, um processo em rascunho é dado administrativo e não deve vazar;
// por isso a política vale para leituras também, no nível da classe.
[Authorize(Roles = "plataforma-admin")]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public; sem isso o MVC ignora a classe e nenhum endpoint é registrado.")]
public sealed class ProcessoSeletivoController : ControllerBase
{
    private const string ResourceTag = "processos-seletivos";

    private readonly ICommandBus _commandBus;
    private readonly IQueryBus _queryBus;
    private readonly IDomainErrorMapper _mapper;
    private readonly IResourceLinksBuilder<ProcessoSeletivoDto> _linksBuilder;

    public ProcessoSeletivoController(
        ICommandBus commandBus,
        IQueryBus queryBus,
        IDomainErrorMapper mapper,
        IResourceLinksBuilder<ProcessoSeletivoDto> linksBuilder)
    {
        _commandBus = commandBus;
        _queryBus = queryBus;
        _mapper = mapper;
        _linksBuilder = linksBuilder;
    }

    [HttpPost]
    [RequiresIdempotencyKey]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Criar([FromBody] CriarProcessoSeletivoCommand command, CancellationToken cancellationToken)
    {
        Result<Guid> resultado = await _commandBus.Send(command, cancellationToken);
        if (resultado.IsSuccess)
            return CreatedAtAction(nameof(ObterPorId), new { id = resultado.Value }, resultado.Value);
        return resultado.ToActionResult(_mapper);
    }

    [HttpGet]
    [VendorMediaType(Resource = "processo-seletivo", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<ProcessoSeletivoResumoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Listar(
        [FromCursor(ResourceTag)] PageRequest page,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);

        ListarProcessosSeletivosResult resultado = await _queryBus.Send(
            new ListarProcessosSeletivosQuery(page.AfterId, page.Limit, page.Direction), cancellationToken);

        return await this.OkPaginatedAsync(
            resultado.Items, resultado.AnteriorAfterId, resultado.ProximoAfterId, page, ResourceTag,
            cancellationToken: cancellationToken);
    }

    [HttpGet("{id:guid}")]
    [VendorMediaType(Resource = "processo-seletivo", Versions = [1])]
    [ProducesResponseType(typeof(ProcessoSeletivoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken cancellationToken)
    {
        ProcessoSeletivoDto? processo = await _queryBus.Send(new ObterProcessoSeletivoQuery(id), cancellationToken);
        if (processo is null)
        {
            return NotFound();
        }

        // HATEOAS Level 1 — ver ADR-0029 para a justificativa completa.
        ProcessoSeletivoDto processoComLinks = processo with { Links = _linksBuilder.Build(processo) };
        return Ok(processoComLinks);
    }

    /// <summary>
    /// Substitui integralmente as etapas pontuadas do processo (CA-02).
    /// </summary>
    [HttpPut("{id:guid}/etapas")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status428PreconditionRequired)]
    [EmiteETag]
    public async Task<IActionResult> DefinirEtapas(
        Guid id,
        [FromBody] IReadOnlyList<EtapaProcessoInput> etapas,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken)
    {
        if (!TentarLerPrecondicao(ifMatch, out PrecondicaoIfMatch precondicao, out IActionResult? malformada))
            return malformada!;

        Result<MutacaoAceita> resultado = await _commandBus.Send(
            new DefinirEtapasCommand(id, etapas, precondicao), cancellationToken);
        return ResponderMutacao(resultado);
    }

    /// <summary>
    /// Define (ou substitui) a oferta de atendimento especializado do
    /// processo (CA-06) — tipo de deficiência só é aceito sob a condição PcD
    /// (ADR-0067).
    /// </summary>
    [HttpPut("{id:guid}/oferta-atendimento")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status428PreconditionRequired)]
    [EmiteETag]
    public async Task<IActionResult> DefinirOfertaAtendimento(
        Guid id,
        [FromBody] DefinirOfertaAtendimentoRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!TentarLerPrecondicao(ifMatch, out PrecondicaoIfMatch precondicao, out IActionResult? malformada))
            return malformada!;

        Result<MutacaoAceita> resultado = await _commandBus.Send(
            new DefinirOfertaAtendimentoCommand(id, request.CondicaoIds, request.RecursoIds, request.TipoDeficienciaIds, precondicao),
            cancellationToken);
        return ResponderMutacao(resultado);
    }

    /// <summary>
    /// Substitui integralmente a distribuição de vagas do processo (Story
    /// #773, modelagem P-A): uma configuração por oferta de curso. O
    /// <c>QuadroDeVagas</c> (quantidade calculada por modalidade) não é
    /// definido aqui — é output derivado de um motor futuro.
    /// </summary>
    [HttpPut("{id:guid}/distribuicao-vagas")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status428PreconditionRequired)]
    [EmiteETag]
    public async Task<IActionResult> DefinirDistribuicaoVagas(
        Guid id,
        [FromBody] IReadOnlyList<ConfiguracaoDistribuicaoVagasInput> distribuicaoVagas,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken)
    {
        if (!TentarLerPrecondicao(ifMatch, out PrecondicaoIfMatch precondicao, out IActionResult? malformada))
            return malformada!;

        Result<MutacaoAceita> resultado = await _commandBus.Send(
            new DefinirDistribuicaoVagasCommand(id, distribuicaoVagas, precondicao), cancellationToken);
        return ResponderMutacao(resultado);
    }

    /// <summary>
    /// Substitui integralmente os critérios de desempate do processo (Story
    /// #774, modelagem P-B §2.6). Dimensão opcional (0..*) — lista vazia
    /// remove todos os critérios.
    /// </summary>
    [HttpPut("{id:guid}/criterios-desempate")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status428PreconditionRequired)]
    [EmiteETag]
    public async Task<IActionResult> DefinirCriteriosDesempate(
        Guid id,
        [FromBody] IReadOnlyList<CriterioDesempateInput> criterios,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken)
    {
        if (!TentarLerPrecondicao(ifMatch, out PrecondicaoIfMatch precondicao, out IActionResult? malformada))
            return malformada!;

        Result<MutacaoAceita> resultado = await _commandBus.Send(
            new DefinirCriteriosDesempateCommand(id, criterios, precondicao), cancellationToken);
        return ResponderMutacao(resultado);
    }

    /// <summary>
    /// Define (ou remove) o bônus regional do processo (RN05, Story #774).
    /// <c>RegraCodigo</c> nulo remove o bônus — a ausência já é o toggle "sem
    /// bônus" (INV-B5).
    /// </summary>
    [HttpPut("{id:guid}/bonus-regional")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status428PreconditionRequired)]
    [EmiteETag]
    public async Task<IActionResult> DefinirBonusRegional(
        Guid id,
        [FromBody] DefinirBonusRegionalRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!TentarLerPrecondicao(ifMatch, out PrecondicaoIfMatch precondicao, out IActionResult? malformada))
            return malformada!;

        Result<MutacaoAceita> resultado = await _commandBus.Send(
            new DefinirBonusRegionalCommand(id, request.RegraCodigo, request.RegraVersao, request.Fator, request.Teto, request.MunicipioConvenio, request.BaseLegal, precondicao),
            cancellationToken);
        return ResponderMutacao(resultado);
    }

    /// <summary>
    /// Substitui integralmente a configuração de classificação do processo
    /// (Story #775, modelagem P-B §2.1) — o 15º bloco canônico, que compõe
    /// por referência a fórmula da nota, a precisão, a lista de eliminação e
    /// a ordem de alocação. Bônus e desempate não são parâmetros aqui.
    /// </summary>
    [HttpPut("{id:guid}/classificacao")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status428PreconditionRequired)]
    [EmiteETag]
    public async Task<IActionResult> DefinirClassificacao(
        Guid id,
        [FromBody] DefinirClassificacaoRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!TentarLerPrecondicao(ifMatch, out PrecondicaoIfMatch precondicao, out IActionResult? malformada))
            return malformada!;

        Result<MutacaoAceita> resultado = await _commandBus.Send(
            new DefinirClassificacaoCommand(
                id,
                request.RegraCalculoCodigo,
                request.RegraCalculoVersao,
                request.RegraArredondamentoCodigo,
                request.RegraArredondamentoVersao,
                request.CasasArredondamento,
                request.RegraOrdemAlocacaoCodigo,
                request.RegraOrdemAlocacaoVersao,
                request.NOpcoesAlocacao,
                request.RegrasEliminacao,
                precondicao),
            cancellationToken);
        return ResponderMutacao(resultado);
    }

    /// <summary>
    /// Publica o processo (RN08): valida a conformidade, congela a versão 1 da
    /// configuração (append-only) e transita o status para Publicado, tudo na mesma
    /// transação — junto da requisição durável que registra o ato em Publicações
    /// (ADR-0108). Quando a publicação é recusada por conformidade insuficiente
    /// (CA-03), o corpo do 422 carrega <c>Extensions["pendencias"]</c> com o checklist.
    /// <para>
    /// O bloco <c>ato</c> é o MESMO que a retificação recebe: o tipo do ato vem
    /// declarado pelo operador e é conferido contra o catálogo de Publicações — nunca
    /// inferido do contexto. Retificar não é um tipo de ato, é uma relação entre atos
    /// (ADR-0103).
    /// </para>
    /// </summary>
    [HttpPost("{id:guid}/publicacao")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
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
                MapearAto(request.Ato)),
            cancellationToken);
        if (resultado.IsSuccess)
        {
            return NoContent();
        }

        IActionResult actionResult = resultado.ToActionResult(_mapper);
        if (!resultado.HasErrorCode("ProcessoSeletivo.ConformidadeInsuficiente")
            || actionResult is not ObjectResult { Value: ProblemDetails problem })
        {
            return actionResult;
        }

        // CA-03: enriquece o 422 de conformidade insuficiente com o checklist
        // de pendências — reconsulta ObterConformidadeProcessoSeletivoQuery
        // (mesmo ProcessoSeletivo.AvaliarConformidade() por trás do gate de
        // Publicar, sem duplicar a regra) para montar Extensions["pendencias"].
        ConformidadeProcessoSeletivoDto? conformidade = await _queryBus
            .Send(new ObterConformidadeProcessoSeletivoQuery(id), cancellationToken)
            .ConfigureAwait(false);
        if (conformidade is not null)
        {
            problem.Extensions["pendencias"] = conformidade.Itens
                .Where(item => !item.Ok)
                .Select(item => item.Item)
                .ToArray();
        }

        return actionResult;
    }

    /// <summary>
    /// Retifica o processo já publicado (RN08, ADR-0101/0103): registra um novo ato,
    /// que emenda o ato criador da versão corrente e a sucede — a versão anterior
    /// permanece imutável. O motivo é obrigatório.
    /// <para>
    /// O corpo é o de <see cref="Publicar"/> mais o <c>motivo</c>: o alvo da retificação
    /// NÃO é informado pelo cliente — o servidor o infere do topo da cadeia de versões
    /// (ADR-0101). E o tipo do ato continua vindo declarado: uma convocação retificada
    /// continua convocação.
    /// </para>
    /// </summary>
    [HttpPost("{id:guid}/retificacoes")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Retificar(
        Guid id,
        [FromBody] RetificarProcessoSeletivoRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result resultado = await _commandBus.Send(
            new RetificarProcessoSeletivoCommand(
                id,
                request.Motivo,
                request.Numero,
                request.PeriodoInscricaoInicio,
                request.PeriodoInscricaoFim,
                request.DocumentoEditalId,
                MapearAto(request.Ato)),
            cancellationToken);

        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Abre a <b>sessão editorial</b> de retificação (ADR-0110 D3): o certame publicado
    /// volta a aceitar os seis <c>Definir*</c>, <b>sem</b> mudar de status e <b>sem</b>
    /// congelar nada. A versão nova nasce só no fechamento.
    /// </summary>
    /// <remarks>
    /// Devolve o <c>ETag</c> da sessão recém-criada — o cliente já sai apto a mutar, sem um
    /// <c>GET</c> no meio.
    /// </remarks>
    [HttpPost("{id:guid}/retificacao-em-curso")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(typeof(RetificacaoEmCursoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [EmiteETag]
    public async Task<IActionResult> AbrirRetificacao(
        Guid id,
        [FromBody] AbrirRetificacaoRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<RetificacaoEmCursoDto> resultado = await _commandBus.Send(
            new AbrirRetificacaoCommand(id, request.Motivo), cancellationToken);

        if (resultado.IsFailure)
        {
            return resultado.ToActionResult(_mapper);
        }

        RetificacaoEmCursoDto rascunho = resultado.Value!;
        Response.Headers.ETag = rascunho.ETag;
        return CreatedAtAction(nameof(ObterRetificacaoEmCurso), new { id }, rascunho);
    }

    /// <summary>
    /// Consulta a sessão editorial em curso — é por aqui que o cliente <b>relê o ETag</b>
    /// depois de um 412.
    /// </summary>
    [HttpGet("{id:guid}/retificacao-em-curso")]
    [VendorMediaType(Resource = "retificacao-em-curso", Versions = [1])]
    [ProducesResponseType(typeof(RetificacaoEmCursoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [EmiteETag]
    public async Task<IActionResult> ObterRetificacaoEmCurso(Guid id, CancellationToken cancellationToken)
    {
        RetificacaoEmCursoDto? rascunho = await _queryBus
            .Send(new ObterRetificacaoEmCursoQuery(id), cancellationToken)
            .ConfigureAwait(false);
        if (rascunho is null)
        {
            return NotFound();
        }

        Response.Headers.ETag = rascunho.ETag;
        return Ok(rascunho);
    }

    /// <summary>
    /// Altera o motivo da sessão editorial em curso — mutação como qualquer outra: exige a
    /// precondição e devolve o <c>ETag</c> novo.
    /// </summary>
    /// <remarks>
    /// Aqui o <c>If-Match</c> é <b>sempre</b> obrigatório, e não condicional como nos seis
    /// <c>Definir*</c>: esta rota só existe <b>para</b> a sessão. Um cliente que a chama sem
    /// precondição cometeu falha de protocolo, e recebe <b>428</b> — antes mesmo de o
    /// servidor conferir se há sessão (ADR-0110 D9, precedência "3 antes de 10").
    /// </remarks>
    [HttpPut("{id:guid}/retificacao-em-curso")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status428PreconditionRequired)]
    [EmiteETag]
    [PrecondicaoObrigatoria]
    public async Task<IActionResult> AlterarMotivoRetificacao(
        Guid id,
        [FromBody] AlterarMotivoRetificacaoRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!TentarLerPrecondicao(ifMatch, out PrecondicaoIfMatch precondicao, out IActionResult? malformada))
            return malformada!;

        Result<MutacaoAceita> resultado = await _commandBus.Send(
            new AlterarMotivoRetificacaoCommand(id, request.Motivo, precondicao), cancellationToken);
        return ResponderMutacao(resultado);
    }

    /// <summary>
    /// Consulta a conformidade estrutural do processo (CA-07): checklist com
    /// cada item obrigatório marcado ok/pendente, sem alterar o processo.
    /// </summary>
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
    /// Resolve o snapshot congelado vigente do processo num instante (RN08,
    /// ADR-0075/0076/0104): a VERSÃO da configuração de maior
    /// <c>vigente_a_partir_de</c> ≤ o instante, desempatada pelo número da
    /// versão. Quem ordena é o relógio do sistema, não a data que o documento
    /// declara. Quando <paramref name="instante"/> é omitido, usa o relógio do
    /// servidor (ADR-0068). É o contrato de LEITURA que o runtime e os
    /// incrementos downstream consomem — a configuração congelada, não a viva.
    /// 422 (<c>Snapshot.VigenteAusente</c>) quando não há versão vigente ≤ o
    /// instante; 404 quando o processo não existe — nunca retorno silencioso.
    /// </summary>
    private static DadosDoAto MapearAto(DadosDoAtoRequest ato)
    {
        ArgumentNullException.ThrowIfNull(ato);
        return new DadosDoAto(ato.Orgao, ato.Serie, ato.Ano, ato.DataPublicacao, ato.Assinante, ato.TipoAtoCodigo);
    }

    /// <summary>
    /// Decodifica o <c>If-Match</c> — <b>só a sintaxe</b> (ADR-0110 D5). Se ele é
    /// <b>obrigatório</b>, e se ele <b>casa</b>, quem decide é o handler, sob o lock: a
    /// obrigatoriedade depende de haver sessão editorial aberta, e o transporte não carrega
    /// o agregado.
    /// </summary>
    private bool TentarLerPrecondicao(string? ifMatch, out PrecondicaoIfMatch precondicao, out IActionResult? malformada)
    {
        Result<PrecondicaoIfMatch> analise = IfMatchHeader.Analisar(ifMatch);
        if (analise.IsFailure)
        {
            precondicao = PrecondicaoIfMatch.Ausente;
            malformada = analise.ToActionResult(_mapper);
            return false;
        }

        precondicao = analise.Value!;
        malformada = null;
        return true;
    }

    /// <summary>
    /// 204 com o <c>ETag</c> <b>novo</b> quando a mutação correu sob sessão editorial; 204
    /// nu quando o processo está em rascunho (não há sessão, não há tag).
    /// </summary>
    /// <remarks>
    /// Devolver o tag novo é o que permite ao cliente encadear a próxima edição sem um
    /// <c>GET</c> no meio — a revisão acabou de ser incrementada, e o tag que ele tinha em
    /// mãos já não vale.
    /// </remarks>
    private IActionResult ResponderMutacao(Result<MutacaoAceita> resultado)
    {
        if (resultado.IsFailure)
        {
            return resultado.ToActionResult(_mapper);
        }

        if (resultado.Value!.ETag is { } etag)
        {
            Response.Headers.ETag = etag;
        }

        return NoContent();
    }

    [HttpGet("{id:guid}/snapshot-vigente")]
    [VendorMediaType(Resource = "snapshot-vigente-processo-seletivo", Versions = [1])]
    [ProducesResponseType(typeof(SnapshotVigenteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ObterSnapshotVigente(
        Guid id,
        [FromQuery] DateTimeOffset? instante,
        CancellationToken cancellationToken)
    {
        Result<SnapshotVigenteDto> resultado = await _queryBus
            .Send(new ObterSnapshotVigenteQuery(id, instante), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(_mapper);
    }
}

/// <summary>
/// Corpo de <see cref="ProcessoSeletivoController.Publicar"/> — omite
/// <c>ProcessoSeletivoId</c> (vem da rota).
/// </summary>
public sealed record PublicarProcessoSeletivoRequest(
    string? Numero,
    DateOnly PeriodoInscricaoInicio,
    DateOnly PeriodoInscricaoFim,
    Guid DocumentoEditalId,
    DadosDoAtoRequest Ato);

/// <summary>
/// Corpo de <see cref="ProcessoSeletivoController.Retificar"/> — carrega só os
/// dados próprios da retificação. Omite <c>ProcessoSeletivoId</c> (vem da rota)
/// e não recebe id de Edital: o Edital sucedido é o vigente, resolvido no
/// servidor (a retificação endereça o agregado, não uma entidade interna).
/// </summary>
public sealed record RetificarProcessoSeletivoRequest(
    string Motivo,
    string? Numero,
    DateOnly PeriodoInscricaoInicio,
    DateOnly PeriodoInscricaoFim,
    Guid DocumentoEditalId,
    DadosDoAtoRequest Ato);

/// <summary>
/// Dados que o DOCUMENTO declara sobre si — órgão publicador, série, ano, data de
/// publicação, quem assina e o tipo do ato no catálogo de Publicações. Nenhum deles é
/// derivado pelo sistema: a data documental não é o relógio, o assinante não é o usuário
/// autenticado, e o tipo não se infere (ADR-0103 — um aviso pode retificar um edital).
/// </summary>
public sealed record DadosDoAtoRequest(
    string Orgao,
    string Serie,
    int Ano,
    DateOnly DataPublicacao,
    string Assinante,
    string TipoAtoCodigo);

/// <summary>
/// Corpo de <see cref="ProcessoSeletivoController.AbrirRetificacao"/> — só o motivo. A
/// versão que a sessão retifica <b>não</b> é informada pelo cliente: o servidor a infere do
/// topo da cadeia (ADR-0101), sob o mesmo lock que abre o rascunho.
/// </summary>
public sealed record AbrirRetificacaoRequest(string Motivo);

/// <summary>
/// Corpo de <see cref="ProcessoSeletivoController.AlterarMotivoRetificacao"/>.
/// </summary>
public sealed record AlterarMotivoRetificacaoRequest(string Motivo);

/// <summary>
/// Corpo de <see cref="ProcessoSeletivoController.DefinirOfertaAtendimento"/>
/// — omite <c>ProcessoSeletivoId</c> (vem da rota).
/// </summary>
public sealed record DefinirOfertaAtendimentoRequest(
    IReadOnlyList<Guid> CondicaoIds,
    IReadOnlyList<Guid> RecursoIds,
    IReadOnlyList<Guid> TipoDeficienciaIds);

/// <summary>
/// Corpo de <see cref="ProcessoSeletivoController.DefinirBonusRegional"/> —
/// omite <c>ProcessoSeletivoId</c> (vem da rota).
/// </summary>
public sealed record DefinirBonusRegionalRequest(
    string? RegraCodigo,
    string? RegraVersao,
    decimal? Fator,
    decimal? Teto,
    string? MunicipioConvenio,
    string? BaseLegal);

/// <summary>
/// Corpo de <see cref="ProcessoSeletivoController.DefinirClassificacao"/> —
/// omite <c>ProcessoSeletivoId</c> (vem da rota).
/// </summary>
public sealed record DefinirClassificacaoRequest(
    string RegraCalculoCodigo,
    string RegraCalculoVersao,
    string? RegraArredondamentoCodigo,
    string? RegraArredondamentoVersao,
    int? CasasArredondamento,
    string RegraOrdemAlocacaoCodigo,
    string RegraOrdemAlocacaoVersao,
    int NOpcoesAlocacao,
    IReadOnlyList<RegraEliminacaoInput> RegrasEliminacao);
