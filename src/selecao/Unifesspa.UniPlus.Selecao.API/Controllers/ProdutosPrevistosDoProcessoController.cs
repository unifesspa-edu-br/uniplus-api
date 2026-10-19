namespace Unifesspa.UniPlus.Selecao.API.Controllers;

using System.Diagnostics.CodeAnalysis;

using Application.Commands.AtosDoProcesso;
using Application.DTOs;
using Application.Queries.ProdutosPrevistos;

using Contracts.Requests;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Os itens que o edital vigente declara publicar em cada fase e etapa do cronograma, com a situação
/// de cada um na visão administrativa, e a publicação deles.
/// </summary>
[ApiController]
[Route("processos-seletivos/{processoSeletivoId:guid}/produtos-previstos")]
// Mesma justificativa de ProcessoSeletivoController: fluxo administrativo de publicação, sem
// fallback policy — restrito a plataforma-admin.
[Authorize(Roles = "plataforma-admin")]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public; sem isso o MVC ignora a classe e nenhum endpoint é registrado.")]
public sealed class ProdutosPrevistosDoProcessoController : ControllerBase
{
    private readonly ICommandBus _commandBus;
    private readonly IQueryBus _queryBus;
    private readonly IDomainErrorMapper _mapper;

    public ProdutosPrevistosDoProcessoController(ICommandBus commandBus, IQueryBus queryBus, IDomainErrorMapper mapper)
    {
        _commandBus = commandBus;
        _queryBus = queryBus;
        _mapper = mapper;
    }

    /// <summary>
    /// Lista os itens previstos no edital vigente, agrupados pela fase do cronograma. Cada item diz
    /// se está aberto, pendente ou publicado e, quando não pode ser publicado agora, o motivo.
    /// </summary>
    [HttpGet]
    [VendorMediaType(Resource = "produtos-previstos-do-processo", Versions = [1])]
    [ProducesResponseType(typeof(ProdutosPrevistosDoProcessoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Listar(Guid processoSeletivoId, CancellationToken cancellationToken)
    {
        Result<ProdutosPrevistosDoProcessoDto> resultado = await _queryBus
            .Send(new ListarProdutosPrevistosDoProcessoQuery(processoSeletivoId), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Publica um item que o edital vigente declara, com o PDF e os anexos já enviados. O administrador
    /// escolhe o item, e o tipo do ato vem dele. Quando o item é resultado, a publicação exige a
    /// confirmação de que o PDF segue a política de divulgação do certame, e a confirmação fica no
    /// registro. A correção de um item publicado, por errata ou retificação do ato, também passa por aqui,
    /// com o ato corrigido e o motivo, e não esbarra na unicidade da primeira publicação.
    /// </summary>
    /// <remarks>
    /// Responde 202: o registro no Publicações e a cópia ao acervo público acontecem depois, por mensagem
    /// durável. O item só conta como publicado quando o Publicações confirma. O definitivo só sai depois
    /// do preliminar da mesma matéria, e o preliminar não se corrige depois que o definitivo que fecha o
    /// ciclo dele foi publicado. Tudo o que o registro central recusaria depois é conferido antes, com 422.
    /// </remarks>
    [HttpPost("{produtoId:guid}/publicacoes")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(typeof(AtoPublicadoDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict,
        Description = "Outra requisição com a mesma Idempotency-Key ainda está em processamento.")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Publicar(
        Guid processoSeletivoId,
        Guid produtoId,
        [FromBody] PublicarProdutoDoEditalRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<AtoPublicadoDto> resultado = await _commandBus
            .Send(
                new PublicarProdutoDoEditalCommand(
                    processoSeletivoId,
                    produtoId,
                    request.Numero,
                    request.DataPublicacao,
                    request.Assinante,
                    request.DocumentoPrincipalId,
                    AnexoDoAtoRequest.ParaComando(request.Anexos),
                    request.PoliticaDeDivulgacaoConfirmada,
                    request.AtoCorrigidoId,
                    request.MotivoDaCorrecao),
                cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? StatusCode(StatusCodes.Status202Accepted, resultado.Value) : resultado.ToActionResult(_mapper);
    }
}
