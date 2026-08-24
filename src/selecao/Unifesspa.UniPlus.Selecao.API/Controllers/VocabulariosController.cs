namespace Unifesspa.UniPlus.Selecao.API.Controllers;

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.Vocabularios;

/// <summary>
/// Vocabulários fechados que a configuração de um Processo Seletivo referencia por código:
/// os fundamentos de isenção de taxa (UNI-REQ-0101) e os campos publicáveis na divulgação
/// de resultado (UNI-REQ-0050).
/// </summary>
/// <remarks>
/// <para>
/// Existem para que o cliente descubra os códigos em runtime. Sem eles, cada tela que monta
/// uma dessas configurações precisa manter sua própria cópia dos tokens, e a cópia
/// envelhece sem avisar — o drift só aparece como requisição recusada, do lado de quem
/// preencheu o formulário corretamente.
/// </para>
/// <para>
/// <b>Somente leitura, e não por falta de tempo.</b> Os dois conjuntos são governados por
/// código: mudam por versão da API, com a mudança de regra que os acompanha, e não por
/// cadastro administrativo. Um CRUD aqui permitiria acrescentar um fundamento que nenhuma
/// verificação sabe conferir, ou um campo de candidato que nenhuma regra de minimização
/// autorizou.
/// </para>
/// <para>
/// A leitura é anônima, como a dos demais catálogos institucionais do módulo: o conteúdo é
/// o vocabulário que o edital publica, sem nenhum dado de candidato.
/// </para>
/// </remarks>
[ApiController]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public; sem isso o MVC ignora a classe.")]
public sealed class VocabulariosController : ControllerBase
{
    private readonly IQueryBus _queryBus;

    public VocabulariosController(IQueryBus queryBus)
    {
        _queryBus = queryBus;
    }

    /// <summary>
    /// Lista os fundamentos de isenção referenciáveis por um processo que cobra taxa, na
    /// ordem canônica. Referenciar um fundamento não decide origem do fato, forma de
    /// comprovação nem quem analisa o pedido — isso pertence à verificação de cada
    /// fundamento, ainda fora desta configuração.
    /// </summary>
    [HttpGet("fundamentos-isencao")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "fundamento-isencao", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<FundamentoIsencaoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ListarFundamentosIsencao(CancellationToken cancellationToken)
    {
        IReadOnlyList<FundamentoIsencaoDto> fundamentos = await _queryBus
            .Send(new ListarFundamentosIsencaoQuery(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(fundamentos);
    }

    /// <summary>
    /// Lista os campos permitidos na divulgação pública de resultado, com o piso que nenhuma
    /// configuração remove e o campo cuja publicação exige justificativa.
    /// </summary>
    [HttpGet("campos-divulgacao")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "campo-divulgacao", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<CampoDivulgacaoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ListarCamposDivulgacao(CancellationToken cancellationToken)
    {
        IReadOnlyList<CampoDivulgacaoDto> campos = await _queryBus
            .Send(new ListarCamposDivulgacaoQuery(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(campos);
    }
}
