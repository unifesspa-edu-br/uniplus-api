namespace Unifesspa.UniPlus.Selecao.API.Controllers;

using System.Diagnostics.CodeAnalysis;

using Application.Commands.ProcessosSeletivos;
using Application.DTOs;
using Application.Queries.ProcessosSeletivos;

using Contracts.Requests;

using Http;

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
using Unifesspa.UniPlus.Selecao.API.Authorization;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Cria, lista e lê o processo seletivo. O administrador cria o processo em rascunho e monta a
/// configuração pelas demais rotas de <c>processos-seletivos</c>, que ficam em controllers próprios
/// da mesma tag.
/// </summary>
/// <remarks>
/// <para>
/// Cada ação de configuração exige a permissão da sua operação na unidade dona do processo
/// (<c>ExigePermissaoNoProcesso</c>), decidida pela concessão e não pelo papel no token.
/// <c>[Authorize]</c> mantém anônimo quem não se autenticou, porque não há fallback policy.
/// </para>
/// <para>
/// A leitura do processo é aberta depois da publicação e restrita a quem configura enquanto é
/// rascunho (<c>ExigeLeituraDoProcesso</c>, anônima). A listagem não tem processo para decidir:
/// devolve os processos publicados e os rascunhos das unidades em que a pessoa configura.
/// </para>
/// </remarks>
[ApiController]
[Route("processos-seletivos")]
[Tags("ProcessoSeletivo")]
[Authorize]
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
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict,
        Description = "O identificador legível informado já é usado por outro processo seletivo (uniplus.selecao.processo_seletivo.identificador_legivel_em_uso). Também responde 409 quando outra requisição com a mesma Idempotency-Key ainda está em processamento.")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Criar([FromBody] CriarProcessoSeletivoCommand command, CancellationToken cancellationToken)
    {
        Result<Guid> resultado = await _commandBus.Send(command, cancellationToken);
        if (resultado.IsSuccess)
            return CreatedAtAction(nameof(ObterPorId), new { id = resultado.Value }, resultado.Value);
        return resultado.ToActionResult(_mapper);
    }

    [AllowAnonymous]
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

    [AllowAnonymous]
    [ExigeLeituraDoProcesso]
    [HttpGet("{id:guid}")]
    [VendorMediaType(Resource = "processo-seletivo", Versions = [1])]
    [ProducesResponseType(typeof(ProcessoSeletivoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken cancellationToken)
    {
        ProcessoSeletivoDto? processo = await _queryBus.Send(new LerProcessoSeletivoQuery(id), cancellationToken);
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
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosConfigurar)]
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
        if (!ProtocoloDeMutacao.TentarLerPrecondicao(_mapper, ifMatch, out PrecondicaoIfMatch precondicao, out IActionResult? malformada))
            return malformada!;

        Result<MutacaoAceita> resultado = await _commandBus.Send(
            new DefinirEtapasCommand(id, etapas, precondicao), cancellationToken);
        return ProtocoloDeMutacao.ResponderMutacao(_mapper, Response, resultado);
    }

    /// <summary>
    /// Substitui as opções que o processo declara para um fato cuja fonte dos valores é o
    /// processo — por exemplo, as edições do ENEM aceitas —, congeladas no edital na publicação
    /// (issue #1619). As opções dos fatos de atendimento especializado vêm da oferta de atendimento.
    /// </summary>
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosConfigurar)]
    [HttpPut("{id:guid}/fatos/{fatoCodigo}/opcoes")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status428PreconditionRequired)]
    [EmiteETag]
    public async Task<IActionResult> DefinirOpcoesDeclaradas(
        Guid id,
        string fatoCodigo,
        [FromBody] DefinirOpcoesDeclaradasRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!ProtocoloDeMutacao.TentarLerPrecondicao(_mapper, ifMatch, out PrecondicaoIfMatch precondicao, out IActionResult? malformada))
            return malformada!;

        Result<MutacaoAceita> resultado = await _commandBus.Send(
            new DefinirOpcoesDeclaradasCommand(
                id,
                fatoCodigo,
                // A nulidade da lista ou de um item segue para o validator, que a recusa com 4xx —
                // desreferenciar aqui responderia 500 antes de o comando existir.
                request.Opcoes is null
                    ? null!
                    : [.. request.Opcoes.Select(static o => o is null ? null! : new OpcaoDeclaradaInput(o.Codigo, o.Rotulo))],
                precondicao),
            cancellationToken);
        return ProtocoloDeMutacao.ResponderMutacao(_mapper, Response, resultado);
    }

    /// <summary>
    /// Define (ou substitui) a oferta de atendimento especializado do
    /// processo (CA-06) — tipo de deficiência só é aceito sob a condição PcD
    /// (ADR-0067).
    /// </summary>
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosConfigurar)]
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

        if (!ProtocoloDeMutacao.TentarLerPrecondicao(_mapper, ifMatch, out PrecondicaoIfMatch precondicao, out IActionResult? malformada))
            return malformada!;

        Result<MutacaoAceita> resultado = await _commandBus.Send(
            new DefinirOfertaAtendimentoCommand(id, request.CondicaoIds, request.RecursoIds, request.TipoDeficienciaIds, precondicao),
            cancellationToken);
        return ProtocoloDeMutacao.ResponderMutacao(_mapper, Response, resultado);
    }

    /// <summary>
    /// Substitui integralmente a distribuição de vagas do processo (Story
    /// #773): uma configuração por oferta de curso. O <c>QuadroDeVagas</c>
    /// (quantidade calculada por modalidade — Lei 12.711 no ramo federal,
    /// fixa no institucional) é materializado na mesma operação pelo motor
    /// de cálculo da issue #848, e devolvido no corpo da resposta (issue
    /// #1283) — o mesmo shape que <see cref="SimularDistribuicaoVagas"/>
    /// (preview sem persistir) e o <c>GET</c> do processo devolvem.
    /// </summary>
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosConfigurar)]
    [HttpPut("{id:guid}/distribuicao-vagas")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(typeof(IReadOnlyList<ConfiguracaoDistribuicaoVagasDto>), StatusCodes.Status200OK)]
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
        if (!ProtocoloDeMutacao.TentarLerPrecondicao(_mapper, ifMatch, out PrecondicaoIfMatch precondicao, out IActionResult? malformada))
            return malformada!;

        Result<MutacaoComDistribuicaoVagasDto> resultado = await _commandBus.Send(
            new DefinirDistribuicaoVagasCommand(id, distribuicaoVagas, precondicao), cancellationToken);
        return ResponderDefinicaoDistribuicaoVagas(resultado);
    }

    /// <summary>
    /// 200 com o quadro de vagas recém-persistido e o <c>ETag</c> <b>novo</b>
    /// quando a mutação correu sob sessão editorial; 200 com o quadro mas
    /// sem header <c>ETag</c> quando o processo está em rascunho (não há
    /// sessão, não há tag) — issue #1283.
    /// </summary>
    /// <remarks>
    /// Diferente de <see cref="ProtocoloDeMutacao.ResponderMutacao"/> (204 sempre, corpo vazio):
    /// aqui o corpo nunca é vazio em caso de sucesso. Deliberadamente não
    /// reaproveita <see cref="ProtocoloDeMutacao.ResponderMutacao"/> nem estende
    /// <see cref="MutacaoAceita"/> — esse tipo e esse helper são
    /// compartilhados por todos os outros <c>Definir*</c> do controller, que
    /// continuam em 204; mudar o shape deles quebraria os demais.
    /// </remarks>
    private IActionResult ResponderDefinicaoDistribuicaoVagas(Result<MutacaoComDistribuicaoVagasDto> resultado)
    {
        if (resultado.IsFailure)
        {
            return resultado.ToActionResult(_mapper);
        }

        ProtocoloDeMutacao.EmitirETag(Response, resultado.Value!.ETag);
        return Ok(resultado.Value!.DistribuicaoVagas);
    }

    /// <summary>
    /// Calcula, sem persistir, o quadro de vagas por modalidade que o
    /// <see cref="DefinirDistribuicaoVagas"/> produziria para o mesmo
    /// payload (issue #1282) — preview para o admin conferir/ajustar
    /// VoBase, PR ou modalidades antes de confirmar a gravação real.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Despachado via <see cref="_queryBus"/>, não <see cref="_commandBus"/>:
    /// é uma leitura (CQRS), nunca uma mutação. POST é só o veículo — o
    /// payload (Guids em lista, quadro declarado) é grande demais para
    /// querystring de um GET idiomático. Por isso não leva
    /// <c>[RequiresIdempotencyKey]</c> (reservado a mutação com efeito
    /// cumulativo, ADR-0027) nem <c>[EmiteETag]</c> (não há concorrência
    /// otimista sobre nada persistido).
    /// </para>
    /// <para>
    /// <b>Ids do corpo da resposta são efêmeros</b> — gerados em memória para
    /// a simulação, nunca gravados; não referenciam nenhum recurso real e
    /// mudam a cada chamada, mesmo com o mesmo payload.
    /// </para>
    /// <para>
    /// <b>Cada oferta é validada isoladamente</b> — as mesmas invariantes que
    /// <c>ConfiguracaoDistribuicaoVagas.Criar</c> aplica por item. Invariantes
    /// de nível de agregado, que só <see cref="DefinirDistribuicaoVagas"/>
    /// verifica com a lista inteira do processo (ex.: oferta duplicada entre
    /// itens do payload, <c>AcaoQuandoIndeferido</c> inconsistente entre
    /// ofertas do mesmo processo), não são checadas aqui — só apareceriam no
    /// PUT real. Decisão de escopo da issue #1282, não lacuna.
    /// </para>
    /// </remarks>
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosConfigurar)]
    [HttpPost("{id:guid}/distribuicao-vagas/simulacao")]
    [VendorMediaType(Resource = "simulacao-distribuicao-vagas", Versions = [1])]
    [ProducesResponseType(typeof(IReadOnlyList<ConfiguracaoDistribuicaoVagasDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SimularDistribuicaoVagas(
        Guid id,
        [FromBody] IReadOnlyList<ConfiguracaoDistribuicaoVagasInput> distribuicaoVagas,
        CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<ConfiguracaoDistribuicaoVagasDto>> resultado = await _queryBus
            .Send(new SimularDistribuicaoVagasQuery(id, distribuicaoVagas), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess
            ? Ok(resultado.Value)
            : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Pré-visualiza o processo diante de um perfil simulado de candidato (UNI-REQ-0144,
    /// UNI-REQ-0145, UNI-REQ-0064): o que cada formulário mostra e exige e os documentos que a
    /// árvore de exigências pediria. Avalia a configuração viva — rascunho ou sessão de
    /// retificação —, não o envelope publicado, e não grava nada: o perfil, que carrega fatos
    /// sensíveis simulados, não é persistido nem registrado.
    /// </summary>
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosConfigurar)]
    [HttpPost("{id:guid}/pre-visualizacao")]
    [VendorMediaType(Resource = "pre-visualizacao-processo-seletivo", Versions = [1])]
    [ProducesResponseType(typeof(PreVisualizacaoDoProcessoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> PreVisualizar(
        Guid id,
        [FromBody] PreVisualizacaoDoProcessoInput simulacao,
        CancellationToken cancellationToken)
    {
        Result<PreVisualizacaoDoProcessoDto> resultado = await _queryBus
            .Send(new PreVisualizarProcessoSeletivoQuery(id, simulacao), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess
            ? Ok(resultado.Value)
            : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Substitui integralmente os critérios de desempate do processo (Story
    /// #774). Lista vazia remove todos os critérios: o rascunho aceita, mas a publicação de
    /// processo com inscrição própria exige ao menos um (resultado importado dispensa).
    /// </summary>
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosConfigurar)]
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
        if (!ProtocoloDeMutacao.TentarLerPrecondicao(_mapper, ifMatch, out PrecondicaoIfMatch precondicao, out IActionResult? malformada))
            return malformada!;

        Result<MutacaoAceita> resultado = await _commandBus.Send(
            new DefinirCriteriosDesempateCommand(id, criterios, precondicao), cancellationToken);
        return ProtocoloDeMutacao.ResponderMutacao(_mapper, Response, resultado);
    }

    /// <summary>
    /// Declara se o processo aplica o bônus regional (Fator de Atenuação das Desigualdades de Acesso, UNI-REQ-0015, Story #774). <c>Aplica</c> falso
    /// dispensa a configuração; verdadeiro exige <c>RegraCodigo</c> e os demais campos do bônus.
    /// A declaração é obrigatória para publicar.
    /// </summary>
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosConfigurar)]
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

        if (!ProtocoloDeMutacao.TentarLerPrecondicao(_mapper, ifMatch, out PrecondicaoIfMatch precondicao, out IActionResult? malformada))
            return malformada!;

        Result<MutacaoAceita> resultado = await _commandBus.Send(
            new DefinirBonusRegionalCommand(id, request.Aplica, request.RegraCodigo, request.RegraVersao, request.Fator, request.Teto, request.FatoElegibilidadeCodigo, request.BaseLegalBonusRegionalId, precondicao),
            cancellationToken);
        return ProtocoloDeMutacao.ResponderMutacao(_mapper, Response, resultado);
    }

    /// <summary>
    /// Declara quantos cursos o candidato escolhe na inscrição (1 ou 2). A declaração é obrigatória
    /// para publicar, e o número não pode superar o de ofertas da distribuição de vagas.
    /// </summary>
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosConfigurar)]
    [HttpPut("{id:guid}/opcoes-de-curso")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status428PreconditionRequired)]
    [EmiteETag]
    public async Task<IActionResult> DefinirOpcoesDeCurso(
        Guid id,
        [FromBody] DefinirOpcoesDeCursoRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!ProtocoloDeMutacao.TentarLerPrecondicao(_mapper, ifMatch, out PrecondicaoIfMatch precondicao, out IActionResult? malformada))
            return malformada!;

        Result<MutacaoAceita> resultado = await _commandBus.Send(
            new DefinirOpcoesDeCursoCommand(id, request.Numero, precondicao),
            cancellationToken);
        return ProtocoloDeMutacao.ResponderMutacao(_mapper, Response, resultado);
    }

    /// <summary>
    /// Define (ou remove) a taxa de inscrição e os fundamentos de isenção do processo (issue
    /// #1112). <c>Cobra</c> nulo remove a declaração — o processo volta a "ainda não
    /// declarado", que BLOQUEIA a publicação (CA-01).
    /// </summary>
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosConfigurar)]
    [HttpPut("{id:guid}/taxa-inscricao")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status428PreconditionRequired)]
    [EmiteETag]
    public async Task<IActionResult> DefinirTaxaInscricao(
        Guid id,
        [FromBody] DefinirTaxaInscricaoRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!ProtocoloDeMutacao.TentarLerPrecondicao(_mapper, ifMatch, out PrecondicaoIfMatch precondicao, out IActionResult? malformada))
            return malformada!;

        Result<MutacaoAceita> resultado = await _commandBus.Send(
            new DefinirTaxaInscricaoCommand(id, request.Cobra, request.Valor, request.Fundamentos, precondicao),
            cancellationToken);
        return ProtocoloDeMutacao.ResponderMutacao(_mapper, Response, resultado);
    }

    /// <summary>
    /// Define (ou remove) a divulgação pública do processo (UNI-REQ-0050, issue #563).
    /// <c>CamposPublicos</c> nulo remove a configuração explícita — o processo volta ao
    /// default minimizado (só o número de inscrição).
    /// </summary>
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosConfigurar)]
    [HttpPut("{id:guid}/divulgacao")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status428PreconditionRequired)]
    [EmiteETag]
    public async Task<IActionResult> DefinirConfiguracaoDivulgacao(
        Guid id,
        [FromBody] DefinirConfiguracaoDivulgacaoRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!ProtocoloDeMutacao.TentarLerPrecondicao(_mapper, ifMatch, out PrecondicaoIfMatch precondicao, out IActionResult? malformada))
            return malformada!;

        Result<MutacaoAceita> resultado = await _commandBus.Send(
            new DefinirConfiguracaoDivulgacaoCommand(id, request.CamposPublicos, request.Justificativa, precondicao),
            cancellationToken);
        return ProtocoloDeMutacao.ResponderMutacao(_mapper, Response, resultado);
    }

    /// <summary>
    /// Define (ou remove) a cascata de remanejamento do processo
    /// (cascata de remanejamento de cotas, UNI-REQ-0056, Story #575). <c>RegraCodigo</c> nulo remove a
    /// cascata — a ausência já é o toggle "sem cascata".
    /// </summary>
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosConfigurar)]
    [HttpPut("{id:guid}/cascata-remanejamento")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status428PreconditionRequired)]
    [EmiteETag]
    public async Task<IActionResult> DefinirCascataRemanejamento(
        Guid id,
        [FromBody] DefinirCascataRemanejamentoRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!ProtocoloDeMutacao.TentarLerPrecondicao(_mapper, ifMatch, out PrecondicaoIfMatch precondicao, out IActionResult? malformada))
            return malformada!;

        Result<MutacaoAceita> resultado = await _commandBus.Send(
            new DefinirCascataRemanejamentoCommand(id, request.RegraCodigo, request.RegraVersao, request.FallbackCodigo, request.Destinos, precondicao),
            cancellationToken);
        return ProtocoloDeMutacao.ResponderMutacao(_mapper, Response, resultado);
    }

    /// <summary>
    /// Substitui integralmente a configuração de classificação do processo
    /// (Story #775) — o 15º bloco canônico, que compõe
    /// por referência a fórmula da nota, a precisão, a lista de eliminação e
    /// a ordem de alocação. Bônus e desempate não são parâmetros aqui.
    /// </summary>
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosConfigurar)]
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

        if (!ProtocoloDeMutacao.TentarLerPrecondicao(_mapper, ifMatch, out PrecondicaoIfMatch precondicao, out IActionResult? malformada))
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
                request.RegrasEliminacao,
                request.BaseadoEmEnem,
                request.ResolucaoPesoAreaEnem,
                request.EscalaNotaFinal,
                precondicao),
            cancellationToken);
        return ProtocoloDeMutacao.ResponderMutacao(_mapper, Response, resultado);
    }

    /// <summary>
    /// Substitui integralmente o cronograma de fases do processo (Story #851, CA-06):
    /// janela, produtos publicados com o papel de cada um, fase que conclui o ciclo
    /// recursal, parecer individual, bancas requeridas e regra de recurso, por fase. O
    /// <c>GET</c> vem do endpoint agregado
    /// (<see cref="ObterPorId"/>), que passa a devolver <c>CronogramaFases</c> como parte
    /// do recurso — não há rota aninhada própria de leitura.
    /// </summary>
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosConfigurar)]
    [HttpPut("{id:guid}/cronograma-fases")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status428PreconditionRequired)]
    [EmiteETag]
    public async Task<IActionResult> DefinirCronogramaFases(
        Guid id,
        [FromBody] IReadOnlyList<FaseCronogramaInput> fases,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken)
    {
        if (!ProtocoloDeMutacao.TentarLerPrecondicao(_mapper, ifMatch, out PrecondicaoIfMatch precondicao, out IActionResult? malformada))
            return malformada!;

        Result<MutacaoAceita> resultado = await _commandBus.Send(
            new DefinirCronogramaFasesCommand(id, fases, precondicao), cancellationToken);
        return ProtocoloDeMutacao.ResponderMutacao(_mapper, Response, resultado);
    }

    /// <summary>
    /// Substitui integralmente os documentos exigidos do processo (Story #554): fase,
    /// snapshot-copy do tipo de documento, aplicabilidade GERAL/CONDICIONAL,
    /// obrigatoriedade, consequência de indeferimento, grupo de satisfação, o gatilho
    /// DNF (dinâmico/multivalorado para MODALIDADE/CONDICAO_ATENDIMENTO, PR #896), a base
    /// legal 1:N (PR #898) — validada apenas na FORMA aqui; o gate "≥1 RESOLVIDO por
    /// exigência que determina resultado" é da publicação (ADR-0074) — e a idade máxima
    /// de emissão/formato/tamanho do arquivo (PR #900, issue #893) — aviso, não bloqueio de
    /// presença; congelados por chamada (substituição integral, não <i>merge</i> parcial).
    /// O <c>GET</c> vem do endpoint agregado (<see cref="ObterPorId"/>) — não há rota
    /// aninhada própria de leitura.
    /// </summary>
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosConfigurar)]
    [HttpPut("{id:guid}/documentos-exigidos")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status428PreconditionRequired)]
    [EmiteETag]
    public async Task<IActionResult> DefinirDocumentosExigidos(
        Guid id,
        [FromBody] IReadOnlyList<NoExigenciaInput> raizes,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken)
    {
        if (!ProtocoloDeMutacao.TentarLerPrecondicao(_mapper, ifMatch, out PrecondicaoIfMatch precondicao, out IActionResult? malformada))
            return malformada!;

        Result<MutacaoAceita> resultado = await _commandBus.Send(
            new DefinirDocumentosExigidosCommand(id, raizes, precondicao), cancellationToken);
        return ProtocoloDeMutacao.ResponderMutacao(_mapper, Response, resultado);
    }

    /// <summary>
    /// Declara ou troca o identificador legível do certame, de onde derivam o endereço
    /// público e a chave no acervo. Aceito enquanto ele não consta em versão publicada: em
    /// rascunho, ou na sessão de retificação aberta sobre versão congelada sem ele.
    /// </summary>
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosConfigurar)]
    [HttpPut("{id:guid}/identificador-legivel")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict,
        Description = "O identificador legível informado já é usado por outro processo seletivo (uniplus.selecao.processo_seletivo.identificador_legivel_em_uso). Também responde 409 quando outra requisição com a mesma Idempotency-Key ainda está em processamento.")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status428PreconditionRequired)]
    [EmiteETag]
    public async Task<IActionResult> DefinirIdentificadorLegivel(
        Guid id,
        [FromBody] DefinirIdentificadorLegivelRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!ProtocoloDeMutacao.TentarLerPrecondicao(_mapper, ifMatch, out PrecondicaoIfMatch precondicao, out IActionResult? malformada))
            return malformada!;

        Result<MutacaoAceita> resultado = await _commandBus.Send(
            new DefinirIdentificadorLegivelCommand(id, request.IdentificadorLegivel, precondicao),
            cancellationToken);
        return ProtocoloDeMutacao.ResponderMutacao(_mapper, Response, resultado);
    }

    /// <summary>
    /// Define (ou remove) a política que ancora <c>FAIXA_ETARIA</c> na publicação (Story
    /// #554, PR #896 — B-03 do plano). <c>Tipo</c> nulo remove a referência.
    /// </summary>
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
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status428PreconditionRequired)]
    [EmiteETag]
    public async Task<IActionResult> DefinirLocalidade(
        Guid id,
        [FromBody] DefinirLocalidadeRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!ProtocoloDeMutacao.TentarLerPrecondicao(_mapper, ifMatch, out PrecondicaoIfMatch precondicao, out IActionResult? malformada))
            return malformada!;

        Result<MutacaoAceita> resultado = await _commandBus.Send(
            new DefinirLocalidadeCommand(id, request.CodigoIbge, request.Nome, request.Uf, precondicao),
            cancellationToken);
        return ProtocoloDeMutacao.ResponderMutacao(_mapper, Response, resultado);
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
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status428PreconditionRequired)]
    [EmiteETag]
    public async Task<IActionResult> DefinirAlgoritmoContagemPrazo(
        Guid id,
        [FromBody] DefinirAlgoritmoContagemPrazoRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!ProtocoloDeMutacao.TentarLerPrecondicao(_mapper, ifMatch, out PrecondicaoIfMatch precondicao, out IActionResult? malformada))
            return malformada!;

        Result<MutacaoAceita> resultado = await _commandBus.Send(
            new DefinirAlgoritmoContagemPrazoCommand(id, request.Codigo, request.Versao, precondicao),
            cancellationToken);
        return ProtocoloDeMutacao.ResponderMutacao(_mapper, Response, resultado);
    }

    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosConfigurar)]
    [HttpPut("{id:guid}/referencia-temporal-fatos")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status428PreconditionRequired)]
    [EmiteETag]
    public async Task<IActionResult> DefinirReferenciaTemporalFatos(
        Guid id,
        [FromBody] DefinirReferenciaTemporalFatosRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!ProtocoloDeMutacao.TentarLerPrecondicao(_mapper, ifMatch, out PrecondicaoIfMatch precondicao, out IActionResult? malformada))
            return malformada!;

        Result<MutacaoAceita> resultado = await _commandBus.Send(
            new DefinirReferenciaTemporalFatosCommand(id, request.Tipo, request.Data, request.FaseId, precondicao),
            cancellationToken);
        return ProtocoloDeMutacao.ResponderMutacao(_mapper, Response, resultado);
    }

    /// <summary>
    /// Substitui integralmente as regras que derivam os fatos derivados do processo — hoje a
    /// modalidade de concorrência (Story #985). Cada configuração traz o fato derivado e a lista de
    /// regras <c>{ ordem, contribui, quando? }</c>; a regra incondicional (âncora) tem <c>quando</c>
    /// nulo. Só um fato derivado com binding de regra de derivação é alvo válido — para a
    /// modalidade, cada código contribuído tem de ser uma das modalidades ofertadas pelo processo.
    /// Editável em rascunho e sob sessão de retificação (Story #986): em rascunho <c>204</c> sem
    /// ETag e <c>If-Match</c> ignorado; sob sessão, <c>If-Match</c> obrigatório (<c>428</c>/<c>412</c>)
    /// e <c>204</c> com o ETag novo. Processo publicado sem sessão é recusado com <c>422</c>.
    /// </summary>
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosConfigurar)]
    [HttpPut("{id:guid}/regras-derivacao")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status428PreconditionRequired)]
    [EmiteETag]
    public async Task<IActionResult> DefinirRegrasDerivacao(
        Guid id,
        [FromBody] IReadOnlyList<ConfiguracaoDerivacaoInput> configuracoes,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken)
    {
        if (!ProtocoloDeMutacao.TentarLerPrecondicao(_mapper, ifMatch, out PrecondicaoIfMatch precondicao, out IActionResult? malformada))
            return malformada!;

        Result<MutacaoAceita> resultado = await _commandBus.Send(
            new DefinirRegrasDerivacaoCommand(id, configuracoes, precondicao), cancellationToken);
        return ProtocoloDeMutacao.ResponderMutacao(_mapper, Response, resultado);
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
