namespace Unifesspa.UniPlus.Autorizacao.API.Controllers;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Queries.Pedidos;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Pedidos de concessão de acesso sob <c>/api/autorizacao/pedidos-de-concessao</c>: toda concessão nasce de um pedido
/// que o gestor de acessos e, para o perfil de dado pessoal ou sensível, a proteção de dados aprovam.
/// </summary>
/// <remarks>
/// Qualquer pessoa autenticada pede: pedir não dá acesso, o filtro é a aprovação. As rotas de decisão exigem só
/// autenticação, porque o handler descobre a etapa pela situação do pedido e confere a permissão dela pelo ponto de
/// decisão único: sem a da etapa e com a da outra a recusa é de etapa, sem nenhuma das duas é <c>403</c>, e a
/// identidade incompleta é <c>401</c>. O pedido que a pessoa não pode ver responde <c>404</c>.
/// </remarks>
[ApiController]
[Authorize]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public.")]
public sealed class PedidosDeConcessaoController : ControllerBase
{
    private const string ResourceTag = "pedidos-de-concessao";
    private const string PerfisParaPedidoTag = "perfis-para-pedido";

    private readonly ICommandBus _commandBus;
    private readonly IQueryBus _queryBus;
    private readonly IDomainErrorMapper _mapper;

    public PedidosDeConcessaoController(ICommandBus commandBus, IQueryBus queryBus, IDomainErrorMapper mapper)
    {
        _commandBus = commandBus;
        _queryBus = queryBus;
        _mapper = mapper;
    }

    /// <summary>
    /// Cria o pedido. O beneficiário é o próprio solicitante (e-mail em branco ou o da sessão verificado) ou outra
    /// pessoa, nomeada pelo e-mail institucional; neste caso o pedido espera o aceite dela.
    /// </summary>
    [HttpPost("pedidos-de-concessao")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(typeof(PedidoDeConcessaoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Criar([FromBody] CriarPedidoDeConcessaoRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<PedidoDeConcessaoDto> resultado = await _commandBus
            .Send(
                new CriarPedidoDeConcessaoCommand(request.BeneficiarioEmail, request.PerfilId, request.UnidadeId, request.Justificativa),
                cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess
            ? CreatedAtAction(nameof(Obter), new { id = resultado.Value!.Id }, resultado.Value)
            : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Lista os pedidos, paginados por cursor opaco bidirecional (ADR-0026 + ADR-0089). <c>fila=gestor</c> e
    /// <c>fila=protecao_de_dados</c> são as filas dos aprovadores e exigem a permissão da etapa; <c>fila=todos</c> lista os
    /// pedidos em qualquer situação e exige a permissão de uma das duas etapas; <c>fila=meus</c>, o padrão, são os
    /// pedidos de quem pergunta.
    /// </summary>
    [HttpGet("pedidos-de-concessao")]
    [VendorMediaType(Resource = "pedido-de-concessao", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<PedidoResumoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    public async Task<IActionResult> Listar(
        [FromCursor(ResourceTag, RequireSortKey = true)] PageRequest page,
        [FromQuery] string? fila = null,
        [FromQuery] string? situacao = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);

        FilaDePedidos? filaLida = LerFila(fila);
        if (filaLida is null)
        {
            return Recusar(PedidoErrorCodes.FilaInvalida, "Fila inválida. Use meus, gestor, protecao_de_dados ou todos.");
        }

        // A âncora do cursor é o par (instante, Id) e o instante chega como texto: decodificar o wire é do boundary, e um
        // cursor adulterado vira 400 em vez de 500.
        if (page.AfterSortKey is { } ancora
            && !DateTimeOffset.TryParse(ancora, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _))
        {
            return Recusar(PedidoErrorCodes.CursorInvalido, "O cursor informado é inválido.");
        }

        SituacaoDoPedido? situacaoLida = null;
        if (!string.IsNullOrWhiteSpace(situacao))
        {
            if (!TentarLerSituacao(situacao, out SituacaoDoPedido lida))
            {
                return Recusar(PedidoErrorCodes.FiltroDeSituacaoInvalido, "Situação inválida. Use aguardando_documento, aguardando_beneficiario, aguardando_gestor, aguardando_protecao_de_dados, aprovado, recusado ou cancelado.");
            }

            situacaoLida = lida;
        }

        Result<ListarPedidosDeConcessaoResult> resultado = await _queryBus.Send(
            new ListarPedidosDeConcessaoQuery(filaLida.Value, situacaoLida, page.AfterSortKey, page.AfterId, page.Limit, page.Direction),
            cancellationToken).ConfigureAwait(false);
        if (resultado.IsFailure)
        {
            return resultado.ToActionResult(_mapper);
        }

        return await this.OkPaginatedOrdenadoAsync(
            resultado.Value!.Items,
            resultado.Value.Anterior,
            resultado.Value.Proximo,
            page,
            ResourceTag,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Os perfis ativos que se podem pedir, só com o necessário para escolher, paginados por cursor opaco bidirecional
    /// (ADR-0026 + ADR-0089). Lê quem tem e-mail institucional verificado ou a permissão de manter perfis; o usuário
    /// externo sem ela recebe 403.
    /// </summary>
    [HttpGet("perfis-para-pedido")]
    [VendorMediaType(Resource = "perfil-para-pedido", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<PerfilParaPedidoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    public async Task<IActionResult> PerfisParaPedir(
        [FromCursor(PerfisParaPedidoTag)] PageRequest page,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);

        Result<ListarPerfisParaPedirResult> resultado = await _queryBus
            .Send(new ListarPerfisParaPedirQuery(page.AfterId, page.Limit, page.Direction), cancellationToken)
            .ConfigureAwait(false);
        if (resultado.IsFailure)
        {
            return resultado.ToActionResult(_mapper);
        }

        return await this.OkPaginatedAsync(
            resultado.Value!.Items,
            resultado.Value.AnteriorAfterId,
            resultado.Value.ProximoAfterId,
            page,
            PerfisParaPedidoTag,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Lê o pedido: só as partes e quem decide uma etapa ou administra concessões o veem; os demais recebem 404.</summary>
    [HttpGet("pedidos-de-concessao/{id:guid}")]
    [VendorMediaType(Resource = "pedido-de-concessao", Versions = [1])]
    [ProducesResponseType(typeof(PedidoDeConcessaoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> Obter(Guid id, CancellationToken cancellationToken)
    {
        Result<PedidoDeConcessaoDto> resultado = await _queryBus
            .Send(new ObterPedidoDeConcessaoQuery(id), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(_mapper);
    }

    /// <summary>Trilha do pedido: quem pediu, aceitou, anexou, leu, aprovou, recusou ou cancelou, e a dispensa da segunda aprovação.</summary>
    [HttpGet("pedidos-de-concessao/{id:guid}/trilha")]
    [VendorMediaType(Resource = "trilha-do-pedido", Versions = [1])]
    [ProducesResponseType(typeof(IReadOnlyList<EventoDaTrilhaDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> Trilha(Guid id, CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<EventoDaTrilhaDto>> resultado = await _queryBus
            .Send(new ObterTrilhaDoPedidoQuery(id), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(_mapper);
    }

    /// <summary>O beneficiário aceita o pedido que o nomeou; o e-mail verificado do token tem de ser o do pedido.</summary>
    [HttpPost("pedidos-de-concessao/{id:guid}/aceite")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(typeof(PedidoDeConcessaoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Aceitar(Guid id, CancellationToken cancellationToken)
    {
        Result<PedidoDeConcessaoDto> resultado = await _commandBus
            .Send(new AceitarPedidoDeConcessaoCommand(id), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(_mapper);
    }

    /// <summary>Pede a URL de PUT assinada de um PDF para anexar ao pedido, só do solicitante e antes da primeira decisão.</summary>
    [HttpPost("pedidos-de-concessao/{id:guid}/anexos")]
    // TTL alinhado ao da URL de PUT devolvida: um replay depois de ela expirar devolveria uma URL inutilizável.
    [RequiresIdempotencyKey(TtlSeconds = 15 * 60)]
    [ProducesResponseType(typeof(AnexoParaEnvioDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> EnviarAnexo(
        Guid id, [FromBody] EnviarAnexoDoPedidoRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<AnexoParaEnvioDto> resultado = await _commandBus
            .Send(new CriarAnexoDoPedidoCommand(id, request.TamanhoDoPdf), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess
            ? CreatedAtAction(nameof(LerAnexo), new { id, anexoId = resultado.Value!.Id }, resultado.Value)
            : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// O solicitante informa o <c>ETag</c> que o PUT devolveu. A API não lê o objeto: grava o anexo como em verificação
    /// e publica o pedido ao verificador na mesma transação.
    /// </summary>
    [HttpPost("pedidos-de-concessao/{id:guid}/anexos/{anexoId:guid}/confirmacao")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(typeof(AnexoDoPedidoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ConfirmarAnexo(
        Guid id, Guid anexoId, [FromBody] ConfirmarAnexoDoPedidoRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<AnexoDoPedidoDto> resultado = await _commandBus
            .Send(new ConfirmarAnexoDoPedidoCommand(id, anexoId, request.Etag), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// URL assinada de leitura do anexo, de validade curta, só para o anexo verificado e só para quem vê o pedido. O
    /// acesso entra na trilha.
    /// </summary>
    [HttpGet("pedidos-de-concessao/{id:guid}/anexos/{anexoId:guid}")]
    [VendorMediaType(Resource = "anexo-do-pedido", Versions = [1])]
    [ProducesResponseType(typeof(LeituraDoAnexoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> LerAnexo(Guid id, Guid anexoId, CancellationToken cancellationToken)
    {
        Result<LeituraDoAnexoDto> resultado = await _commandBus
            .Send(new LerAnexoDoPedidoCommand(id, anexoId), cancellationToken)
            .ConfigureAwait(false);
        if (resultado.IsFailure)
        {
            return resultado.ToActionResult(_mapper);
        }

        // A URL é credencial de acesso: nenhum cache a guarda.
        Response.Headers.CacheControl = "no-store";

        return Ok(resultado.Value);
    }

    /// <summary>
    /// Aprova a etapa que o pedido aguarda. A concessão nasce na última aprovação exigida. Ninguém decide pedido em
    /// que é parte, e quem decidiu como gestor não decide na etapa de proteção de dados.
    /// </summary>
    [HttpPost("pedidos-de-concessao/{id:guid}/aprovacoes")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(typeof(PedidoDeConcessaoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Aprovar(Guid id, CancellationToken cancellationToken)
    {
        Result<PedidoDeConcessaoDto> resultado = await _commandBus
            .Send(new AprovarPedidoDeConcessaoCommand(id), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(_mapper);
    }

    /// <summary>Recusa o pedido na etapa que ele aguarda, com motivo. Encerra o pedido sem concessão.</summary>
    [HttpPost("pedidos-de-concessao/{id:guid}/recusa")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(typeof(PedidoDeConcessaoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Recusar(
        Guid id, [FromBody] RecusarPedidoDeConcessaoRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<PedidoDeConcessaoDto> resultado = await _commandBus
            .Send(new RecusarPedidoDeConcessaoCommand(id, request.Motivo), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(_mapper);
    }

    /// <summary>Cancela o pedido: só o solicitante, antes da primeira decisão. O pedido sai das filas.</summary>
    [HttpDelete("pedidos-de-concessao/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Cancelar(Guid id, CancellationToken cancellationToken)
    {
        Result resultado = await _commandBus
            .Send(new CancelarPedidoDeConcessaoCommand(id), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }

    private static FilaDePedidos? LerFila(string? texto) =>
        string.IsNullOrWhiteSpace(texto)
            ? FilaDePedidos.Meus
            : texto.Trim().ToLowerInvariant() switch
            {
                "meus" => FilaDePedidos.Meus,
                "gestor" => FilaDePedidos.Gestor,
                "protecao_de_dados" => FilaDePedidos.ProtecaoDeDados,
                "todos" => FilaDePedidos.Todos,
                _ => null,
            };

    // Só o vocabulário publicado vale: o nome do membro do enum não é contrato.
    private static bool TentarLerSituacao(string texto, out SituacaoDoPedido situacao)
    {
        try
        {
            situacao = SituacaoDoPedidoCodigo.FromCodigo(texto.Trim().ToLowerInvariant());
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            situacao = default;
            return false;
        }
    }

    private IActionResult Recusar(string codigo, string mensagem) =>
        Result.Failure(new DomainError(codigo, mensagem)).ToActionResult(_mapper);
}
