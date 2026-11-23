namespace Unifesspa.UniPlus.Configuracao.API.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.FatosCandidato;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Permissoes;
using Unifesspa.UniPlus.Configuracao.Application.Queries.FatosCandidato;
using Unifesspa.UniPlus.Infrastructure.Core.Authorization;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Endpoints públicos de leitura do catálogo <c>dados_do_candidato</c> — o catálogo de fatos do candidato
/// (UNI-REQ-0077, UNI-REQ-0143, ADR-0136) —, e a manutenção pelo administrador do cadastro de fatos, com a permissão de
/// manter: cadastro de fato declarado e de derivado por regra, edição de nome e descrição, definição, regras padrão
/// do derivado, desativação e reativação. Os valores de domínio e a proteção de dados do fato têm controller próprio.
/// </summary>
[ApiController]
[Authorize]
[Tags("FatosCandidato")]
public sealed class FatosCandidatoController : ControllerBase
{
    private const string ResourceTagAdmin = "admin-fatos-candidato";

    private readonly ICommandBus _commandBus;
    private readonly IQueryBus _queryBus;
    private readonly IDomainErrorMapper _mapper;

    public FatosCandidatoController(ICommandBus commandBus, IQueryBus queryBus, IDomainErrorMapper mapper)
    {
        _commandBus = commandBus;
        _queryBus = queryBus;
        _mapper = mapper;
    }

    /// <summary>
    /// Lista o catálogo de fatos do candidato, ordenado por código, cada fato com os operadores que uma condição
    /// pode usar sobre ele. É de baixo volume, portanto não paginado; a manutenção tem lista própria, paginada e
    /// filtrável.
    /// </summary>
    [HttpGet("fatos-candidato")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "fato-candidato", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<FatoDoCatalogoDto>), StatusCodes.Status200OK)]
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
    [ProducesResponseType(typeof(FatoDoCatalogoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ObterPorCodigo(string codigo, CancellationToken cancellationToken)
    {
        FatoDoCatalogoDto? fato = await _queryBus
            .Send(new ObterFatoCandidatoPorCodigoQuery(codigo), cancellationToken)
            .ConfigureAwait(false);

        return fato is null ? NotFound() : Ok(fato);
    }

    /// <summary>Lista de manutenção, paginada, com filtro opcional por origem e por estado ativo.</summary>
    [HttpGet("admin/fatos-candidato")]
    [ExigePermissao<PermissaoConfiguracao>(PermissaoConfiguracao.FatosCandidatoConsultar, ResourceTipo = ResourceTiposConfiguracao.FatoCandidato)]
    [VendorMediaType(Resource = "fato-candidato", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<FatoDeManutencaoDto>), StatusCodes.Status200OK)]
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
    [ProducesResponseType(typeof(FatoDeManutencaoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ObterParaManutencao(Guid id, CancellationToken cancellationToken)
    {
        FatoDeManutencaoDto? fato = await _queryBus.Send(new ObterFatoCandidatoParaManutencaoQuery(id), cancellationToken)
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
    /// <remarks>
    /// O corpo é plano e cada campo vem da <c>definicao</c> da resposta do fato: <c>dominio</c>, <c>cardinalidade</c>,
    /// <c>formato</c> e <c>escopo</c> têm o mesmo nome; <c>fonteValores</c> é <c>definicao.valores.fonte</c>;
    /// <c>pontoResolucao</c> é <c>definicao.resolucao.pontoResolucao</c>; <c>classificacaoProtecao</c>,
    /// <c>finalidadeTratamento</c> e <c>hipoteseLegal</c> são <c>definicao.protecao.classificacao</c>,
    /// <c>definicao.protecao.finalidade</c> e <c>definicao.protecao.hipoteseLegal</c>.
    /// </remarks>
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

    /// <summary>
    /// Edita o nome, a descrição e o componente de exibição; os eixos têm a alteração própria, em <c>/definicao</c>.
    /// O corpo tem os campos do <c>descritivo</c> da resposta do fato.
    /// </summary>
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

    private async Task<IActionResult> Enviar(ICommand<Result> command, CancellationToken cancellationToken)
    {
        Result resultado = await _commandBus.Send(command, cancellationToken).ConfigureAwait(false);
        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }
}
