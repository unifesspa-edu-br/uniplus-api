namespace Unifesspa.UniPlus.Selecao.API.Controllers;

using System.Diagnostics.CodeAnalysis;

using Application.DTOs;
using Application.Permissoes;
using Application.Queries.CpfDaInscricao;
using Application.Queries.DocumentosDaInscricao;
using Application.Queries.InscricoesDoProcesso;
using Application.Services;

using Authorization;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// As inscrições do processo na visão de quem o opera. A permissão de consultá-las vale na unidade dona do processo,
/// e a pessoa sem ela recebe o mesmo 404 do processo que não existe. Consultar as inscrições não abre os documentos:
/// cada classe de documento tem a sua permissão.
/// </summary>
[ApiController]
[Route("processos-seletivos/{id:guid}/inscricoes")]
[Authorize]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public; sem isso o MVC ignora a classe e nenhum endpoint é registrado.")]
public sealed class InscricoesDoProcessoController : ControllerBase
{
    private const string ResourceTag = "inscricoes-do-processo";

    private readonly IQueryBus _queryBus;
    private readonly IDomainErrorMapper _mapper;

    public InscricoesDoProcessoController(IQueryBus queryBus, IDomainErrorMapper mapper)
    {
        _queryBus = queryBus;
        _mapper = mapper;
    }

    /// <summary>
    /// Lista as inscrições enviadas do processo, da mais antiga para a mais recente, com o CPF mascarado
    /// (<c>***.999.999-**</c>). O rascunho não aparece: o candidato ainda não se inscreveu.
    /// </summary>
    [HttpGet]
    [ExigePermissaoNoProcesso(PermissaoSelecao.InscricoesConsultar, RecursoTipo = AcessoAoProcesso.RecursoInscricao)]
    [VendorMediaType(Resource = "inscricao-do-processo", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<InscricaoDoProcessoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Listar(Guid id, [FromCursor(ResourceTag)] PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);

        ListarInscricoesDoProcessoResult resultado = await _queryBus
            .Send(new ListarInscricoesDoProcessoQuery(id, page.AfterId, page.Limit, page.Direction), cancellationToken)
            .ConfigureAwait(false);

        return await this.OkPaginatedAsync(
            resultado.Items, resultado.AnteriorAfterId, resultado.ProximoAfterId, page, ResourceTag,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Revela o CPF completo de UMA inscrição enviada, pela permissão própria de revelar CPF na unidade dona do processo;
    /// consultar as inscrições não basta. É POST e sem corpo para que nenhum intermediário o pré-carregue ou o guarde, e
    /// sem chave de idempotência porque a resposta, com o CPF, nunca deve ser reaproveitada de cache. Cada revelação
    /// permitida fica no registro de decisões de acesso com a inscrição e a concessão usada. A inscrição que não existe,
    /// a de outro processo, a em rascunho e a recusa por falta de permissão respondem o mesmo 404.
    /// </summary>
    [HttpPost("{inscricaoId:guid}/cpf/revelacao")]
    [VendorMediaType(Resource = "cpf-revelado-da-inscricao", Versions = [1])]
    [ProducesResponseType(typeof(CpfReveladoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> RevelarCpf(Guid id, Guid inscricaoId, CancellationToken cancellationToken)
    {
        Result<CpfReveladoDto> resultado = await _queryBus
            .Send(new RevelarCpfDaInscricaoQuery(id, inscricaoId), cancellationToken)
            .ConfigureAwait(false);
        if (!resultado.IsSuccess)
        {
            return resultado.ToActionResult(_mapper);
        }

        Response.Headers.CacheControl = "no-store";
        return Ok(resultado.Value);
    }

    /// <summary>
    /// Emite a URL assinada de curta validade para baixar o arquivo verificado de uma inscrição. A permissão exigida é
    /// a da classificação congelada da exigência a que o arquivo responde: documentos sensíveis ou documentos pessoais.
    /// O arquivo que não existe, o de outro processo e o que a pessoa não pode abrir respondem o mesmo 404. A URL é
    /// credencial de acesso, então a resposta vai com <c>no-store</c>.
    /// </summary>
    [HttpGet("{inscricaoId:guid}/documentos/arquivos/{arquivoId:guid}/acesso")]
    [VendorMediaType(Resource = "acesso-arquivo-da-inscricao", Versions = [1])]
    [ProducesResponseType(typeof(AcessoArquivoDoCandidatoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AcessoAoDocumento(Guid id, Guid inscricaoId, Guid arquivoId, CancellationToken cancellationToken)
    {
        Result<AcessoArquivoDoCandidatoDto> resultado = await _queryBus
            .Send(new ObterAcessoAoDocumentoDaInscricaoQuery(id, inscricaoId, arquivoId), cancellationToken)
            .ConfigureAwait(false);
        if (!resultado.IsSuccess)
        {
            return resultado.ToActionResult(_mapper);
        }

        Response.Headers.CacheControl = "no-store";
        return Ok(resultado.Value);
    }
}
