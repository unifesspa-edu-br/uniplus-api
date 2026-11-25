namespace Unifesspa.UniPlus.Configuracao.API.Controllers;

using System.Collections.Generic;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Application.Queries.Vocabularios;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;

/// <summary>
/// Vocabulários fechados que a modalidade de concorrência referencia por código: a natureza legal, a composição de vagas, a regra de remanejamento e a ação quando indeferido.
/// </summary>
/// <remarks>
/// Existem para que o cliente descubra os códigos e os rótulos em runtime, sem manter cópia própria. São somente leitura:
/// mudam por versão da API, com a regra que os acompanha, e não por cadastro. A leitura é anônima porque a das
/// modalidades já é anônima, e o vocabulário não traz dado de pessoa.
/// </remarks>
[ApiController]
[Tags("Vocabularios")]
public sealed class VocabulariosDaModalidadeController : ControllerBase
{
    private readonly IQueryBus _queryBus;

    public VocabulariosDaModalidadeController(IQueryBus queryBus)
    {
        _queryBus = queryBus;
    }

    /// <summary>Lista as naturezas legais, com código e rótulo, na ordem de exibição.</summary>
    [HttpGet("vocabularios/naturezas-legais")]
    [EndpointName("ListarNaturezasLegais")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "codigo-natureza-legal", Versions = [1])]
    [ProducesResponseType(typeof(IReadOnlyList<CodigoComRotulo<NaturezaLegal>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ListarNaturezasLegais(CancellationToken cancellationToken)
    {
        IReadOnlyList<CodigoComRotulo<NaturezaLegal>> naturezasLegais = await _queryBus
            .Send(new ListarNaturezasLegaisQuery(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(naturezasLegais);
    }

    /// <summary>Lista as ações quando indeferido, com código e rótulo, na ordem de exibição.</summary>
    [HttpGet("vocabularios/acoes-quando-indeferido")]
    [EndpointName("ListarAcoesQuandoIndeferido")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "codigo-acao-quando-indeferido", Versions = [1])]
    [ProducesResponseType(typeof(IReadOnlyList<CodigoComRotulo<AcaoQuandoIndeferido>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ListarAcoesQuandoIndeferido(CancellationToken cancellationToken)
    {
        IReadOnlyList<CodigoComRotulo<AcaoQuandoIndeferido>> acoesQuandoIndeferido = await _queryBus
            .Send(new ListarAcoesQuandoIndeferidoQuery(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(acoesQuandoIndeferido);
    }

    /// <summary>Lista as composições de vagas, com código e rótulo, na ordem de exibição.</summary>
    [HttpGet("vocabularios/composicoes-vagas")]
    [EndpointName("ListarComposicoesDasVagas")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "codigo-composicao-vagas", Versions = [1])]
    [ProducesResponseType(typeof(IReadOnlyList<CodigoComRotulo<ComposicaoVagas>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ListarComposicoesVagas(CancellationToken cancellationToken)
    {
        IReadOnlyList<CodigoComRotulo<ComposicaoVagas>> composicoesVagas = await _queryBus
            .Send(new ListarComposicoesVagasQuery(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(composicoesVagas);
    }

    /// <summary>Lista as regras de remanejamento, com código e rótulo, na ordem de exibição.</summary>
    [HttpGet("vocabularios/regras-remanejamento")]
    [EndpointName("ListarRegrasDeRemanejamento")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "codigo-regra-remanejamento", Versions = [1])]
    [ProducesResponseType(typeof(IReadOnlyList<CodigoComRotulo<RegraRemanejamento>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ListarRegrasRemanejamento(CancellationToken cancellationToken)
    {
        IReadOnlyList<CodigoComRotulo<RegraRemanejamento>> regrasRemanejamento = await _queryBus
            .Send(new ListarRegrasRemanejamentoQuery(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(regrasRemanejamento);
    }
}
