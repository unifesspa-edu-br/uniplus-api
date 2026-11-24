namespace Unifesspa.UniPlus.Configuracao.API.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.FatosCandidato;
using Unifesspa.UniPlus.Configuracao.Application.Commands.ProtecaoDoCatalogo;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Permissoes;
using Unifesspa.UniPlus.Configuracao.Application.Queries.ProtecaoDoCatalogo;
using Unifesspa.UniPlus.Infrastructure.Core.Authorization;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Proteção de dados do fato do candidato: o aceite da recomendação de ajuste que o Representante do controlador
/// fez, e a leitura, pelo administrador do cadastro, dos atos e das manifestações técnicas da proteção.
/// </summary>
[ApiController]
[Authorize]
[Tags("FatosCandidato")]
public sealed class FatosCandidatoProtecaoController : ControllerBase
{
    private readonly ICommandBus _commandBus;
    private readonly IQueryBus _queryBus;
    private readonly IDomainErrorMapper _mapper;
    private readonly ITitularesDaPermissao _titulares;

    public FatosCandidatoProtecaoController(ICommandBus commandBus, IQueryBus queryBus, IDomainErrorMapper mapper, ITitularesDaPermissao titulares)
    {
        _commandBus = commandBus;
        _queryBus = queryBus;
        _mapper = mapper;
        _titulares = titulares;
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
    /// de quem se manifestou e a situação de cada uma: vigente, substituída ou encerrada (UNI-REQ-0374). Quem consulta o cadastro de fatos do candidato as lê sem poder se manifestar nem decidir; o Gestor de proteção de dados, pela permissão de consultar a proteção de dados.
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

    private async Task<IActionResult> Enviar(ICommand<Result> command, CancellationToken cancellationToken)
    {
        Result resultado = await _commandBus.Send(command, cancellationToken).ConfigureAwait(false);
        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }
}
