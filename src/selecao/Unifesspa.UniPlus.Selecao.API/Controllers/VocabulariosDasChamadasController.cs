namespace Unifesspa.UniPlus.Selecao.API.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Selecao.Application.Queries.Vocabularios;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.CadastroDeReserva;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Manifestacao;

/// <summary>Vocabulários que o edital referencia ao declarar o que acontece entre uma chamada e outra.</summary>
[ApiController]
[Tags("Vocabularios")]
public sealed class VocabulariosDasChamadasController(IQueryBus queryBus) : ControllerBase
{
    /// <summary>Lista o que a reserva de uma chamada pode fazer quando a chamada a convoca, cada opção com a explicação do que significa.</summary>
    [HttpGet("vocabularios/participacoes-reserva")]
    [EndpointName("ListarParticipacoesDaReserva")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "participacao-da-reserva", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<CodigoComDescricao<ParticipacaoDaReserva>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ListarParticipacoesDaReserva(CancellationToken cancellationToken)
    {
        IReadOnlyList<CodigoComDescricao<ParticipacaoDaReserva>> opcoes = await queryBus
            .Send(new ListarParticipacoesDaReservaQuery(), cancellationToken).ConfigureAwait(false);
        return Ok(opcoes);
    }

    /// <summary>Lista as formas de o edital prever a manifestação de interesse depois da 1ª chamada, cada uma com a explicação do que significa.</summary>
    [HttpGet("vocabularios/manifestacoes-interesse")]
    [EndpointName("ListarManifestacoesDeInteresse")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "manifestacao-de-interesse", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<CodigoComDescricao<ManifestacaoDeInteresse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ListarManifestacoesDeInteresse(CancellationToken cancellationToken)
    {
        IReadOnlyList<CodigoComDescricao<ManifestacaoDeInteresse>> opcoes = await queryBus
            .Send(new ListarManifestacoesDeInteresseQuery(), cancellationToken).ConfigureAwait(false);
        return Ok(opcoes);
    }
}
