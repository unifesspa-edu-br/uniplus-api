namespace Unifesspa.UniPlus.Selecao.API.Controllers;

using System.Diagnostics.CodeAnalysis;

using Application.DTOs;
using Application.DTOs.Comprovantes;
using Application.Permissoes;
using Application.Queries.CampoDaInscricao;
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
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs.Administrativos;

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
    /// Lista as inscrições enviadas do processo, da mais antiga para a mais recente. Cada campo da linha sai pela
    /// permissão da sua classe de dado e é omitido, sem indicação, para quem não a tem: o curso e a situação pelos dados
    /// públicos, o número e a data do envio pelos internos, o nome e o CPF mascarado (<c>***.999.999-**</c>) pelos
    /// pessoais. O rascunho não aparece: o candidato ainda não se inscreveu. Cada inscrição exibida fica no registro de
    /// acessos.
    /// </summary>
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ExigePermissaoNoProcesso(PermissaoSelecao.InscricoesConsultar, Alvo = AlvoDaPermissaoNoProcesso.Inscricoes)]
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
    /// Consulta uma inscrição enviada do processo. O acesso à inscrição é decidido antes da busca, por consultar as
    /// inscrições na unidade dona do processo. A inscrição que não existe, a de outro processo, a em rascunho e a pessoa
    /// sem essa permissão recebem o mesmo 404; quem tem a permissão mas está inscrito no processo recebe 403, e o token
    /// que não identifica o solicitante por completo, 401. Cada dado e cada arquivo sai pela permissão da sua classe e é
    /// omitido, sem indicação, para quem não a tem. O identificador vem só mascarado, com <c>podeRevelar</c>; o valor
    /// completo sai pela revelação do campo. O arquivo traz o identificador usado pelo acesso, sem o conteúdo. O acesso à
    /// inscrição e cada classe permitida ficam no registro de acessos, e a resposta vai com <c>no-store</c>.
    /// </summary>
    /// <param name="id">O processo seletivo.</param>
    /// <param name="inscricaoId">A inscrição.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [HttpGet("{inscricaoId:guid}")]
    [VendorMediaType(Resource = "inscricao-administrativa", Versions = [1])]
    [ProducesResponseType(typeof(InscricaoAdministrativaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> Obter(Guid id, Guid inscricaoId, CancellationToken cancellationToken)
    {
        Result<InscricaoAdministrativaDto> resultado = await _queryBus
            .Send(new ObterInscricaoAdministrativaQuery(id, inscricaoId), cancellationToken)
            .ConfigureAwait(false);
        return resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Revela o valor completo de UM campo identificador de uma inscrição enviada (o CPF, o número do RG, do passaporte e os
    /// demais que o processo classificou assim), pela permissão de revelar identificadores na unidade dona do processo;
    /// consultar as inscrições não basta. O campo é referido pelo código do fato, que não é dado pessoal, e pela ocorrência
    /// quando está num grupo. É POST e sem corpo para que nenhum intermediário o pré-carregue ou o guarde, e sem chave de
    /// idempotência porque a resposta, com o valor, nunca deve ser reaproveitada de cache. Cada revelação, permitida ou
    /// negada, fica no registro de acessos com a inscrição, o código do fato e a ocorrência. A inscrição que não existe, a de
    /// outro processo, a em rascunho, o campo que não é identificador e a recusa por falta de permissão respondem o mesmo 404.
    /// </summary>
    /// <param name="id">O processo seletivo.</param>
    /// <param name="inscricaoId">A inscrição.</param>
    /// <param name="fatoCodigo">O código do fato a revelar.</param>
    /// <param name="ocorrencia">O identificador da ocorrência do grupo, quando o campo está em grupo.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [HttpPost("{inscricaoId:guid}/campos/{fatoCodigo:regex(^[[A-Z]][[A-Z0-9_]]{{0,59}}$)}/revelacao")]
    [VendorMediaType(Resource = "campo-revelado-da-inscricao", Versions = [1])]
    [ProducesResponseType(typeof(CampoReveladoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> RevelarCampo(
        Guid id, Guid inscricaoId, string fatoCodigo, [FromQuery] string? ocorrencia, CancellationToken cancellationToken)
    {
        Result<CampoReveladoDto> resultado = await _queryBus
            .Send(new RevelarCampoDaInscricaoQuery(id, inscricaoId, fatoCodigo, ocorrencia), cancellationToken)
            .ConfigureAwait(false);
        return resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Emite a URL assinada de curta validade para baixar o arquivo verificado de uma inscrição. A permissão exigida é
    /// a da classificação congelada da exigência a que o arquivo responde, uma para cada uma das cinco classes (pública,
    /// interna, pessoal, identificadora e sensível), sem hierarquia entre elas. A classificação que não se resolve vale
    /// como sensível, e consultar as inscrições não abre arquivo algum.
    /// O arquivo que não existe, o de outro processo e o que a pessoa não pode abrir respondem o mesmo 404. A URL é
    /// credencial de acesso, então a resposta vai com <c>no-store</c>.
    /// </summary>
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [HttpGet("{inscricaoId:guid}/documentos/arquivos/{arquivoId:guid}/acesso")]
    [VendorMediaType(Resource = "acesso-arquivo-da-inscricao", Versions = [1])]
    [ProducesResponseType(typeof(AcessoArquivoDoCandidatoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AcessoAoDocumento(Guid id, Guid inscricaoId, Guid arquivoId, CancellationToken cancellationToken)
    {
        Result<AcessoArquivoDoCandidatoDto> resultado = await _queryBus
            .Send(new ObterAcessoAoDocumentoDaInscricaoQuery(id, inscricaoId, arquivoId), cancellationToken)
            .ConfigureAwait(false);
        return resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Emite, pela unidade que opera o processo, o comprovante número <paramref name="numero"/> de uma inscrição do processo,
    /// enviada ou cancelada, no mesmo contrato que o titular lê. Exige consultar as inscrições e emitir o comprovante na
    /// unidade dona do processo. A inscrição que não existe, a de outro processo, a em rascunho e a pessoa sem consultar as
    /// inscrições recebem o mesmo 404, e o comprovante que a inscrição não tem, 404; quem consulta mas não pode emitir recebe
    /// 403. O comprovante da inscrição cancelada traz <c>cancelamento</c>, com o aviso pronto para exibir. A resposta
    /// vai com <c>no-store</c>, e o acesso fica no registro de acessos.
    /// </summary>
    /// <param name="id">O processo seletivo.</param>
    /// <param name="inscricaoId">A inscrição.</param>
    /// <param name="numero">A posição do comprovante, a partir de 1.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [HttpGet("{inscricaoId:guid}/comprovantes/{numero:int}")]
    [VendorMediaType(Resource = "comprovante-inscricao", Versions = [1])]
    [ProducesResponseType(typeof(ComprovanteDaInscricaoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> EmitirComprovante(Guid id, Guid inscricaoId, int numero, CancellationToken cancellationToken)
    {
        Result<ComprovanteDaInscricaoDto> resultado = await _queryBus
            .Send(new EmitirComprovanteDaInscricaoQuery(id, inscricaoId, numero), cancellationToken)
            .ConfigureAwait(false);
        return resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(_mapper);
    }
}
