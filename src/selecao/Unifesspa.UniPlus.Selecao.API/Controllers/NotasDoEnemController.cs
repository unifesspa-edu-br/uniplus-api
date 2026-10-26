namespace Unifesspa.UniPlus.Selecao.API.Controllers;

using System.Diagnostics.CodeAnalysis;

using Application.Commands.NotasDoEnem;
using Application.DTOs.NotasDoEnem;
using Application.Queries.NotasDoEnem;

using Contracts.Requests;

using Domain.Leiautes;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Envio dos CPFs da edição do ENEM ao INEP e importação das notas que o INEP devolve. Restrito a
/// quem configura o processo seletivo.
/// </summary>
[ApiController]
[Authorize(Roles = "plataforma-admin")]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public; sem isso o MVC ignora a classe e nenhum endpoint é registrado.")]
public sealed class NotasDoEnemController : ControllerBase
{
    private const string BaseDaEdicao = "processos-seletivos/{processoSeletivoId:guid}/etapas/{etapaId:guid}/edicoes-do-enem/{edicao}";

    private readonly ICommandBus _commandBus;
    private readonly IQueryBus _queryBus;
    private readonly IDomainErrorMapper _mapper;

    public NotasDoEnemController(ICommandBus commandBus, IQueryBus queryBus, IDomainErrorMapper mapper)
    {
        _commandBus = commandBus;
        _queryBus = queryBus;
        _mapper = mapper;
    }

    /// <summary>
    /// Lista os leiautes de arquivo do INEP ativos, para escolher na geração do envio e na
    /// importação do retorno. Sem o filtro <c>tipo</c> (<c>Envio</c> ou <c>Retorno</c>), devolve os dois tipos.
    /// </summary>
    [HttpGet("leiautes-de-arquivo-do-inep")]
    [VendorMediaType(Resource = "leiaute-de-arquivo-do-inep", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<LeiauteDoInepDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ListarLeiautes(
        [FromQuery(Name = "tipo")] TipoDeLeiauteDoInep? tipo,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<LeiauteDoInepDto> leiautes = await _queryBus
            .Send(new ListarLeiautesDoInepQuery(tipo), cancellationToken)
            .ConfigureAwait(false);

        return Ok(leiautes);
    }

    /// <summary>
    /// Gera o arquivo de envio ao INEP com os CPFs das inscrições enviadas que escolheram a edição, um
    /// por linha e em ordem crescente, no leiaute escolhido. O arquivo é a única saída do sistema com CPF
    /// e vai no corpo da resposta, sem cache; o registro da geração guarda só quem, quando e quantos.
    /// </summary>
    /// <remarks>
    /// Sem <c>Idempotency-Key</c> de propósito: a chave guardaria a resposta, e a resposta é a lista de
    /// CPFs. Repetir a requisição registra outra geração, e cada geração é um ato.
    /// </remarks>
    [HttpPost(BaseDaEdicao + "/envios-ao-inep")]
    [Produces("text/plain")]
    [ProducesResponseType(typeof(byte[]), StatusCodes.Status200OK, "text/plain")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GerarEnvio(
        Guid processoSeletivoId,
        Guid etapaId,
        string edicao,
        [FromBody] GerarEnvioAoInepRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<EnvioAoInepGeradoDto> resultado = await _commandBus
            .Send(
                new GerarEnvioAoInepCommand(processoSeletivoId, etapaId, edicao, request.LeiauteCodigo, request.LeiauteVersao),
                cancellationToken)
            .ConfigureAwait(false);
        if (resultado.IsFailure)
        {
            return resultado.ToActionResult(_mapper);
        }

        Response.Headers.CacheControl = "no-store";
        return File(resultado.Value!.Conteudo, "text/plain; charset=utf-8", resultado.Value.NomeSugerido);
    }

    /// <summary>
    /// Lista as gerações do arquivo de envio ao INEP da edição, da mais recente para a mais antiga: quem
    /// gerou, quando, com que leiaute e quantos CPFs.
    /// </summary>
    [HttpGet(BaseDaEdicao + "/envios-ao-inep")]
    [VendorMediaType(Resource = "envio-ao-inep", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<GeracaoDeEnvioDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ListarEnvios(
        Guid processoSeletivoId, Guid etapaId, string edicao, CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<GeracaoDeEnvioDto>> resultado = await _queryBus
            .Send(new ListarEnviosAoInepQuery(processoSeletivoId, etapaId, edicao), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(_mapper);
    }
}
