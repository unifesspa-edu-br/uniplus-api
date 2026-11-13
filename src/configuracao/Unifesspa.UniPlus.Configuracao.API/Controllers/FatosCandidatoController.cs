namespace Unifesspa.UniPlus.Configuracao.API.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.FatosCandidato;
using Unifesspa.UniPlus.Configuracao.Application.Commands.ProtecaoDoCatalogo;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Permissoes;
using Unifesspa.UniPlus.Configuracao.Application.Queries.FatosCandidato;
using Unifesspa.UniPlus.Configuracao.Application.Queries.ProtecaoDoCatalogo;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Infrastructure.Core.Authorization;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Endpoints públicos de leitura do catálogo <c>rol_de_fatos_candidato</c> — o catálogo de fatos do candidato
/// (UNI-REQ-0077, UNI-REQ-0143, ADR-0136) —, e a manutenção pelo administrador do cadastro de fatos, com a permissão de
/// manter: cadastro de fato declarado e de derivado por regra, edição de nome e descrição, desativação, valores de
/// domínio, regras padrão do derivado e aceite da recomendação de ajuste que o Representante do controlador fez na
/// proteção de dados do fato. O administrador do cadastro lê também os atos e as manifestações técnicas da proteção.
/// </summary>
[ApiController]
[Authorize]
public sealed class FatosCandidatoController : ControllerBase
{
    private const string ResourceTagAdmin = "admin-fatos-candidato";

    private readonly ICommandBus _commandBus;
    private readonly IQueryBus _queryBus;
    private readonly IDomainErrorMapper _mapper;
    private readonly ITitularesDaPermissao _titulares;

    public FatosCandidatoController(ICommandBus commandBus, IQueryBus queryBus, IDomainErrorMapper mapper, ITitularesDaPermissao titulares)
    {
        _titulares = titulares;
        _commandBus = commandBus;
        _queryBus = queryBus;
        _mapper = mapper;
    }

    /// <summary>
    /// Lista o catálogo de fatos do candidato, ordenado por código. É de baixo volume, portanto não
    /// paginado; a manutenção tem lista própria, paginada e filtrável.
    /// </summary>
    [HttpGet("fatos-candidato")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "fato-candidato", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<FatoCandidatoView>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> Listar(CancellationToken cancellationToken)
    {
        ListarFatosCandidatoResult resultado = await _queryBus
            .Send(new ListarFatosCandidatoQuery(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(resultado.Itens);
    }

    /// <summary>
    /// Obtém um fato pela sua chave natural — o código (ex.: <c>COR_RACA</c>).
    /// Retorna 404 quando o código não existe no vocabulário.
    /// </summary>
    [HttpGet("fatos-candidato/{codigo}")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "fato-candidato", Versions = [1])]
    [ProducesResponseType(typeof(FatoCandidatoView), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ObterPorCodigo(string codigo, CancellationToken cancellationToken)
    {
        FatoCandidatoView? fato = await _queryBus
            .Send(new ObterFatoCandidatoPorCodigoQuery(codigo), cancellationToken)
            .ConfigureAwait(false);

        return fato is null ? NotFound() : Ok(fato);
    }

    /// <summary>Lista de manutenção, paginada, com filtro opcional por origem e por estado ativo.</summary>
    [HttpGet("admin/fatos-candidato")]
    [ExigePermissao<PermissaoConfiguracao>(PermissaoConfiguracao.FatosCandidatoConsultar, ResourceTipo = ResourceTiposConfiguracao.FatoCandidato)]
    [VendorMediaType(Resource = "fato-candidato", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<FatoCandidatoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ListarParaManutencao(
        [FromCursor(ResourceTagAdmin)] PageRequest page,
        [FromQuery] string? origem = null,
        [FromQuery] bool? ativo = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);
        ListarFatosCandidatoParaManutencaoResult resultado = await _queryBus.Send(
            new ListarFatosCandidatoParaManutencaoQuery(page.AfterId, page.Limit, page.Direction, origem, ativo), cancellationToken)
            .ConfigureAwait(false);
        return await this.OkPaginatedAsync(
            resultado.Items, resultado.AnteriorAfterId, resultado.ProximoAfterId, page, ResourceTagAdmin, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>O fato para manutenção, ativo ou desativado.</summary>
    [HttpGet("admin/fatos-candidato/{id:guid}")]
    [ExigePermissao<PermissaoConfiguracao>(PermissaoConfiguracao.FatosCandidatoConsultar, ResourceTipo = ResourceTiposConfiguracao.FatoCandidato)]
    [VendorMediaType(Resource = "fato-candidato", Versions = [1])]
    [ProducesResponseType(typeof(FatoCandidatoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ObterParaManutencao(Guid id, CancellationToken cancellationToken)
    {
        FatoCandidatoDto? fato = await _queryBus.Send(new ObterFatoCandidatoParaManutencaoQuery(id), cancellationToken)
            .ConfigureAwait(false);
        return fato is null ? NotFound() : Ok(fato);
    }

    /// <summary>
    /// Cadastra um fato declarado. O código é imutável e nunca reutilizado; o vínculo ao campo de
    /// formulário é gerado a partir dele.
    /// </summary>
    [HttpPost("admin/fatos-candidato")]
    [ExigePermissao<PermissaoConfiguracao>(PermissaoConfiguracao.FatosCandidatoManter, ResourceTipo = ResourceTiposConfiguracao.FatoCandidato)]
    [RequiresIdempotencyKey]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Criar([FromBody] CriarFatoCandidatoCommand command, CancellationToken cancellationToken)
    {
        Result<Guid> resultado = await _commandBus.Send(command, cancellationToken).ConfigureAwait(false);
        return resultado.IsSuccess
            ? CreatedAtAction(nameof(ObterParaManutencao), new { id = resultado.Value }, resultado.Value)
            : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Cadastra um fato derivado por regra, booleano ou categórico. As regras padrão são definidas
    /// depois, quando o categórico já tem os valores que elas contribuem.
    /// </summary>
    [HttpPost("admin/fatos-candidato/derivados")]
    [ExigePermissao<PermissaoConfiguracao>(PermissaoConfiguracao.FatosCandidatoManter, ResourceTipo = ResourceTiposConfiguracao.FatoCandidato)]
    [RequiresIdempotencyKey]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CriarDerivado([FromBody] CriarFatoDerivadoCommand command, CancellationToken cancellationToken)
    {
        Result<Guid> resultado = await _commandBus.Send(command, cancellationToken).ConfigureAwait(false);
        return resultado.IsSuccess
            ? CreatedAtAction(nameof(ObterParaManutencao), new { id = resultado.Value }, resultado.Value)
            : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Cadastra um agregado sobre grupo repetível: o fato do candidato que resume o que os membros
    /// responderam no fato de membro informado.
    /// </summary>
    [HttpPost("admin/fatos-candidato/agregados")]
    [ExigePermissao<PermissaoConfiguracao>(PermissaoConfiguracao.FatosCandidatoManter, ResourceTipo = ResourceTiposConfiguracao.FatoCandidato)]
    [RequiresIdempotencyKey]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CriarAgregado([FromBody] CriarFatoAgregadoCommand command, CancellationToken cancellationToken)
    {
        Result<Guid> resultado = await _commandBus.Send(command, cancellationToken).ConfigureAwait(false);
        return resultado.IsSuccess
            ? CreatedAtAction(nameof(ObterParaManutencao), new { id = resultado.Value }, resultado.Value)
            : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Substitui as regras padrão do derivado por regra, que o processo copia como ponto de partida.
    /// Lista vazia remove as regras.
    /// </summary>
    [HttpPut("admin/fatos-candidato/{id:guid}/regras-padrao")]
    [ExigePermissao<PermissaoConfiguracao>(PermissaoConfiguracao.FatosCandidatoManter, ResourceTipo = ResourceTiposConfiguracao.FatoCandidato)]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> DefinirRegrasPadrao(Guid id, [FromBody] RegrasPadraoInput request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Enviar(new DefinirRegrasPadraoCommand(id, request.Regras), cancellationToken);
    }

    /// <summary>
    /// Altera os eixos do fato que o administrador do cadastro cadastrou — domínio, cardinalidade, fonte dos
    /// valores, formato, escopo, ponto de resolução e proteção de dados —, no subconjunto que o tipo
    /// dele aceita. Código, origem e vínculo não mudam. A alteração que deixaria de valer uma regra
    /// padrão, um agregado, um modelo de formulário ou a regra de uma modalidade é recusada, com a
    /// lista do que deixaria de valer. A versão publicada de um processo não muda.
    /// </summary>
    [HttpPut("admin/fatos-candidato/{id:guid}/definicao")]
    [ExigePermissao<PermissaoConfiguracao>(PermissaoConfiguracao.FatosCandidatoManter, ResourceTipo = ResourceTiposConfiguracao.FatoCandidato)]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> AlterarDefinicao(Guid id, [FromBody] DefinicaoDoFatoInput request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Enviar(new AlterarDefinicaoDoFatoCandidatoCommand(id, request), cancellationToken);
    }

    /// <summary>Edita o nome e a descrição; os eixos têm a alteração própria, em <c>/definicao</c>.</summary>
    [HttpPut("admin/fatos-candidato/{id:guid}")]
    [ExigePermissao<PermissaoConfiguracao>(PermissaoConfiguracao.FatosCandidatoManter, ResourceTipo = ResourceTiposConfiguracao.FatoCandidato)]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> Atualizar(Guid id, [FromBody] DescritivoDoFatoInput request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Enviar(new AtualizarFatoCandidatoCommand(id, request.Nome, request.Descricao, request.ComponenteExibicao), cancellationToken);
    }

    /// <summary>
    /// Aceita a classificação que o Representante do controlador recomendou no ajuste do fato, inclusive o de sistema e o
    /// semeado: o fato fica aprovado com ela, sem nova solicitação, e os Representantes do controlador são avisados.
    /// </summary>
    [HttpPost("admin/fatos-candidato/{id:guid}/aceite-da-recomendacao")]
    [ExigePermissao<PermissaoConfiguracao>(PermissaoConfiguracao.FatosCandidatoManter, ResourceTipo = ResourceTiposConfiguracao.FatoCandidato)]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AceitarRecomendacao(Guid id, CancellationToken cancellationToken)
    {
        // Os destinatários do aviso vêm do módulo Autorização e são lidos antes do handler, que fica num único DbContext.
        IReadOnlyList<string> destinatarios = await _titulares
            .ListarEmailsAsync(PermissaoConfiguracao.ProtecaoDoCatalogoAprovar.Codigo, cancellationToken)
            .ConfigureAwait(false);

        return await Enviar(new AceitarRecomendacaoDoFatoCandidatoCommand(id, destinatarios), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Todos os atos registrados da proteção de dados do fato — solicitação, decisão e aceite —, em ordem cronológica, com o nome de quem agiu. Exige a permissão de consultar o cadastro ou a de consultar a proteção de dados do catálogo. A leitura é registrada antes da resposta; sem o registro, é recusada com 503.
    /// </summary>
    [HttpGet("admin/fatos-candidato/{id:guid}/protecao/atos")]
    [ExigePermissao<PermissaoConfiguracao>(
        PermissaoConfiguracao.FatosCandidatoConsultar,
        ResourceTipo = ResourceTiposConfiguracao.FatoCandidato,
        OuPermissao = PermissaoConfiguracao.ProtecaoDoCatalogoConsultar)]
    [VendorMediaType(Resource = "protecao-do-catalogo-ato", Versions = [1])]
    [ProducesResponseType(typeof(IReadOnlyList<AtoDaProtecaoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ListarAtosDaProtecao(Guid id, CancellationToken cancellationToken)
    {
        IReadOnlyList<AtoDaProtecaoDto>? atos = await _queryBus
            .Send(new ListarAtosDoItemDaProtecaoQuery(CadastroDaProtecaoDoCatalogo.Fato, id), cancellationToken)
            .ConfigureAwait(false);

        return atos is null ? NotFound() : Ok(atos);
    }

    /// <summary>
    /// Todas as manifestações técnicas sobre a proteção de dados do fato do candidato, de todos os ciclos, com o texto, o nome
    /// de quem se manifestou e a situação de cada uma: vigente, substituída ou encerrada (UNI-REQ-0374). Quem consulta o cadastro de fatos do candidato as lê sem poder se manifestar nem decidir; o Encarregado de dados, pela permissão de consultar a proteção de dados.
    /// A leitura é registrada antes da resposta conforme a classe da permissão que a autorizou; sem o registro, é
    /// recusada com 503.
    /// </summary>
    [HttpGet("admin/fatos-candidato/{id:guid}/protecao/manifestacoes")]
    [ExigePermissao<PermissaoConfiguracao>(
        PermissaoConfiguracao.FatosCandidatoConsultar,
        ResourceTipo = ResourceTiposConfiguracao.FatoCandidato,
        OuPermissao = PermissaoConfiguracao.ProtecaoDoCatalogoConsultar)]
    [VendorMediaType(Resource = "protecao-do-catalogo-manifestacao", Versions = [1])]
    [ProducesResponseType(typeof(IReadOnlyList<ManifestacaoDaProtecaoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ListarManifestacoesDaProtecao(Guid id, CancellationToken cancellationToken)
    {
        IReadOnlyList<ManifestacaoDaProtecaoDto>? manifestacoes = await _queryBus
            .Send(new ListarManifestacoesDoItemDaProtecaoQuery(CadastroDaProtecaoDoCatalogo.Fato, id), cancellationToken)
            .ConfigureAwait(false);

        return manifestacoes is null ? NotFound() : Ok(manifestacoes);
    }

    /// <summary>Reativa o fato cadastrado pelo administrador do cadastro, que volta a aceitar vínculos novos.</summary>
    [HttpPost("admin/fatos-candidato/{id:guid}/ativacao")]
    [ExigePermissao<PermissaoConfiguracao>(PermissaoConfiguracao.FatosCandidatoManter, ResourceTipo = ResourceTiposConfiguracao.FatoCandidato)]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> Reativar(Guid id, CancellationToken cancellationToken) =>
        Enviar(new ReativarFatoCandidatoCommand(id), cancellationToken);

    /// <summary>
    /// Desativa o fato cadastrado pelo administrador do cadastro: vínculos novos são recusados, e o processo que já o usa
    /// continua com ele. O fato não é apagado.
    /// </summary>
    [HttpDelete("admin/fatos-candidato/{id:guid}")]
    [ExigePermissao<PermissaoConfiguracao>(PermissaoConfiguracao.FatosCandidatoManter, ResourceTipo = ResourceTiposConfiguracao.FatoCandidato)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> Desativar(Guid id, CancellationToken cancellationToken) =>
        Enviar(new DesativarFatoCandidatoCommand(id), cancellationToken);

    /// <summary>Acrescenta um valor a um fato de fonte global do administrador.</summary>
    [HttpPost("admin/fatos-candidato/{id:guid}/valores")]
    [ExigePermissao<PermissaoConfiguracao>(PermissaoConfiguracao.FatosCandidatoManter, ResourceTipo = ResourceTiposConfiguracao.FatoCandidato)]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> AdicionarValor(Guid id, [FromBody] ValorDominioInput request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Enviar(new AdicionarValorDominioCommand(id, request.Codigo, request.Descricao, request.Ordem, request.Orientacao), cancellationToken);
    }

    /// <summary>
    /// Corrige a descrição e a orientação de um valor do fato cadastrado pelo administrador do cadastro. O código do valor
    /// não muda: mudar o sentido de uma opção é acrescentar outro valor e desativar o antigo.
    /// </summary>
    [HttpPut("admin/fatos-candidato/{id:guid}/valores/{codigo}")]
    [ExigePermissao<PermissaoConfiguracao>(PermissaoConfiguracao.FatosCandidatoManter, ResourceTipo = ResourceTiposConfiguracao.FatoCandidato)]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> CorrigirValor(Guid id, string codigo, [FromBody] CorrecaoDeValorDominioInput request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Enviar(new CorrigirValorDominioCommand(id, codigo, request.Descricao, request.Orientacao), cancellationToken);
    }

    /// <summary>
    /// Reordena os valores do fato cadastrado pelo administrador do cadastro pela lista completa de códigos, ativos e
    /// desativados. Fica fora de <c>valores/</c> porque colidiria com um valor de código ORDEM.
    /// </summary>
    [HttpPut("admin/fatos-candidato/{id:guid}/ordem-dos-valores")]
    [ExigePermissao<PermissaoConfiguracao>(PermissaoConfiguracao.FatosCandidatoManter, ResourceTipo = ResourceTiposConfiguracao.FatoCandidato)]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> ReordenarValores(Guid id, [FromBody] OrdemDosValoresInput request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Enviar(new ReordenarValoresDominioCommand(id, request.Codigos), cancellationToken);
    }

    /// <summary>Reativa um valor do fato cadastrado pelo administrador do cadastro.</summary>
    [HttpPost("admin/fatos-candidato/{id:guid}/valores/{codigo}/ativacao")]
    [ExigePermissao<PermissaoConfiguracao>(PermissaoConfiguracao.FatosCandidatoManter, ResourceTipo = ResourceTiposConfiguracao.FatoCandidato)]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> ReativarValor(Guid id, string codigo, CancellationToken cancellationToken) =>
        Enviar(new ReativarValorDominioCommand(id, codigo), cancellationToken);

    /// <summary>
    /// Desativa um valor do fato cadastrado pelo administrador do cadastro: condição nova que o cite é recusada, e a que já o
    /// citava continua. O código do valor nunca é removido nem renomeado.
    /// </summary>
    [HttpDelete("admin/fatos-candidato/{id:guid}/valores/{codigo}")]
    [ExigePermissao<PermissaoConfiguracao>(PermissaoConfiguracao.FatosCandidatoManter, ResourceTipo = ResourceTiposConfiguracao.FatoCandidato)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> DesativarValor(Guid id, string codigo, CancellationToken cancellationToken) =>
        Enviar(new DesativarValorDominioCommand(id, codigo), cancellationToken);

    private async Task<IActionResult> Enviar(ICommand<Result> command, CancellationToken cancellationToken)
    {
        Result resultado = await _commandBus.Send(command, cancellationToken).ConfigureAwait(false);
        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }
}
