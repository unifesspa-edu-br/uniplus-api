namespace Unifesspa.UniPlus.Configuracao.API.Controllers;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.Commands.FatosCandidato;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Queries.FatosCandidato;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Endpoints públicos de leitura do catálogo <c>rol_de_fatos_candidato</c> — o
/// catálogo de fatos do candidato (UNI-REQ-0077, UNI-REQ-0143, ADR-0136), e a manutenção pelo
/// papel <c>plataforma-admin</c>: cadastro de fato declarado e de derivado por regra, edição de
/// nome e descrição, desativação, valores de domínio e regras padrão do derivado.
/// </summary>
[ApiController]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public.")]
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
    [Authorize(Roles = "plataforma-admin")]
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
    [Authorize(Roles = "plataforma-admin")]
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
    [Authorize(Roles = "plataforma-admin")]
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
    [Authorize(Roles = "plataforma-admin")]
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
    /// Substitui as regras padrão do derivado por regra, que o processo copia como ponto de partida.
    /// Lista vazia remove as regras.
    /// </summary>
    [HttpPut("admin/fatos-candidato/{id:guid}/regras-padrao")]
    [Authorize(Roles = "plataforma-admin")]
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

    /// <summary>Edita o nome e a descrição; os eixos do fato não se editam depois do cadastro.</summary>
    [HttpPut("admin/fatos-candidato/{id:guid}")]
    [Authorize(Roles = "plataforma-admin")]
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
        return Enviar(new AtualizarFatoCandidatoCommand(id, request.Nome, request.Descricao), cancellationToken);
    }

    /// <summary>Reativa o fato do administrador, que volta a aceitar vínculos novos.</summary>
    [HttpPost("admin/fatos-candidato/{id:guid}/ativacao")]
    [Authorize(Roles = "plataforma-admin")]
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
    /// Desativa o fato do administrador: vínculos novos são recusados, e o processo que já o usa
    /// continua com ele. O fato não é apagado.
    /// </summary>
    [HttpDelete("admin/fatos-candidato/{id:guid}")]
    [Authorize(Roles = "plataforma-admin")]
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
    [Authorize(Roles = "plataforma-admin")]
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
        return Enviar(new AdicionarValorDominioCommand(id, request.Codigo, request.Descricao, request.Ordem), cancellationToken);
    }

    /// <summary>Reativa um valor do fato do administrador.</summary>
    [HttpPost("admin/fatos-candidato/{id:guid}/valores/{codigo}/ativacao")]
    [Authorize(Roles = "plataforma-admin")]
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
    /// Desativa um valor do fato do administrador: condição nova que o cite é recusada, e a que já o
    /// citava continua. O código do valor nunca é removido nem renomeado.
    /// </summary>
    [HttpDelete("admin/fatos-candidato/{id:guid}/valores/{codigo}")]
    [Authorize(Roles = "plataforma-admin")]
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
