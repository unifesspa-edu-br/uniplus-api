namespace Unifesspa.UniPlus.Autorizacao.API.Controllers;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Permissoes;
using Unifesspa.UniPlus.Infrastructure.Core.Authorization;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Versões do perfil e o documento que institui cada uma, sob <c>/api/autorizacao/perfis/{id}/versoes</c>.
/// </summary>
/// <remarks>
/// Todas as operações exigem <c>autorizacao:perfis:manter</c>. Versão não se edita: mudar o perfil é criar outra.
/// </remarks>
[ApiController]
[Authorize]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public.")]
public sealed class VersoesDoPerfilController : ControllerBase
{
    private const int TtlDaIdempotenciaEmSegundos = 15 * 60;

    private readonly ICommandBus _commandBus;
    private readonly IDomainErrorMapper _mapper;

    public VersoesDoPerfilController(ICommandBus commandBus, IDomainErrorMapper mapper)
    {
        _commandBus = commandBus;
        _mapper = mapper;
    }

    /// <summary>Cria uma versão pendente do perfil e devolve a URL de PUT do documento que a institui.</summary>
    [HttpPost("perfis/{id:guid}/versoes")]
    [ExigePermissao<PermissaoAutorizacao>(PermissaoAutorizacao.PerfisManter, ResourceTipo = ResourceTiposAutorizacao.Perfil)]
    [RequiresIdempotencyKey(TtlSeconds = TtlDaIdempotenciaEmSegundos)]
    [ProducesResponseType(typeof(EnvioDoDocumentoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Criar(Guid id, [FromBody] DadosDaVersaoRequest dados, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dados);

        Result<EnvioDoDocumentoDto> resultado = await _commandBus
            .Send(new CriarVersaoDoPerfilCommand(id, dados), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, resultado.Value)
            : resultado.ToActionResult(_mapper);
    }

    /// <summary>Cancela a versão pendente do perfil.</summary>
    [HttpDelete("perfis/{id:guid}/versoes/{versaoId:guid}")]
    [ExigePermissao<PermissaoAutorizacao>(PermissaoAutorizacao.PerfisManter, ResourceTipo = ResourceTiposAutorizacao.Perfil)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Cancelar(Guid id, Guid versaoId, CancellationToken cancellationToken)
    {
        Result resultado = await _commandBus
            .Send(new CancelarVersaoDoPerfilCommand(id, versaoId), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }

    /// <summary>Pede a URL de PUT de outro documento, depois que a verificação recusou o anterior.</summary>
    [HttpPost("perfis/{id:guid}/versoes/{versaoId:guid}/documento")]
    [ExigePermissao<PermissaoAutorizacao>(PermissaoAutorizacao.PerfisManter, ResourceTipo = ResourceTiposAutorizacao.Perfil)]
    [RequiresIdempotencyKey(TtlSeconds = TtlDaIdempotenciaEmSegundos)]
    [ProducesResponseType(typeof(EnvioDoDocumentoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> NovoDocumento(
        Guid id, Guid versaoId, [FromBody] EnviarDocumentoDaVersaoRequest dados, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dados);

        Result<EnvioDoDocumentoDto> resultado = await _commandBus
            .Send(new CriarDocumentoDaVersaoCommand(id, versaoId, dados.TamanhoDoDocumento), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, resultado.Value)
            : resultado.ToActionResult(_mapper);
    }

    /// <summary>Pede outra URL de PUT para o documento que ainda aguarda o envio.</summary>
    [HttpPost("perfis/{id:guid}/versoes/{versaoId:guid}/documento/envio")]
    [ExigePermissao<PermissaoAutorizacao>(PermissaoAutorizacao.PerfisManter, ResourceTipo = ResourceTiposAutorizacao.Perfil)]
    [RequiresIdempotencyKey(TtlSeconds = TtlDaIdempotenciaEmSegundos)]
    [ProducesResponseType(typeof(EnvioDoDocumentoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ReemitirEnvio(Guid id, Guid versaoId, CancellationToken cancellationToken)
    {
        Result<EnvioDoDocumentoDto> resultado = await _commandBus
            .Send(new ReemitirEnvioDoDocumentoDaVersaoCommand(id, versaoId), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, resultado.Value)
            : resultado.ToActionResult(_mapper);
    }

    /// <summary>Confirma o PUT com o ETag devolvido; o PDF segue para a verificação.</summary>
    [HttpPost("perfis/{id:guid}/versoes/{versaoId:guid}/documento/confirmacao")]
    [ExigePermissao<PermissaoAutorizacao>(PermissaoAutorizacao.PerfisManter, ResourceTipo = ResourceTiposAutorizacao.Perfil)]
    [ProducesResponseType(typeof(DocumentoDaVersaoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Confirmar(
        Guid id, Guid versaoId, [FromBody] ConfirmarDocumentoDaVersaoRequest dados, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dados);

        Result<DocumentoDaVersaoDto> resultado = await _commandBus
            .Send(new ConfirmarDocumentoDaVersaoCommand(id, versaoId, dados.Etag), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(_mapper);
    }

    /// <summary>Devolve a URL assinada de leitura do documento verificado; o acesso entra na trilha.</summary>
    [HttpGet("perfis/{id:guid}/versoes/{versaoId:guid}/documento")]
    [ExigePermissao<PermissaoAutorizacao>(PermissaoAutorizacao.PerfisManter, ResourceTipo = ResourceTiposAutorizacao.Perfil)]
    [ProducesResponseType(typeof(LeituraDoDocumentoDaVersaoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Ler(Guid id, Guid versaoId, CancellationToken cancellationToken)
    {
        Result<LeituraDoDocumentoDaVersaoDto> resultado = await _commandBus
            .Send(new LerDocumentoDaVersaoCommand(id, versaoId), cancellationToken)
            .ConfigureAwait(false);
        if (resultado.IsFailure)
        {
            return resultado.ToActionResult(_mapper);
        }

        // A URL é credencial de acesso: nenhum cache a guarda.
        Response.Headers.CacheControl = "no-store";

        return Ok(resultado.Value);
    }
}
