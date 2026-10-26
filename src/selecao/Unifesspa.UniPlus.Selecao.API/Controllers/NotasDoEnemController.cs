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
using Unifesspa.UniPlus.Infrastructure.Core.OpenApi;
using Unifesspa.UniPlus.Selecao.API.Http;
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
    private const string CampoDoArquivo = "arquivo";
    private const string CampoDoLeiauteCodigo = "leiauteCodigo";
    private const string CampoDoLeiauteVersao = "leiauteVersao";

    private static readonly string[] TiposDeConteudoAceitos =
    [
        "text/plain", "text/csv", "application/csv", "application/vnd.ms-excel", "application/octet-stream",
    ];

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

    /// <summary>
    /// Importa o arquivo de retorno do INEP da edição, no leiaute escolhido: confere o arquivo contra a
    /// estrutura do leiaute e, se confere, grava um lote novo que passa a valer por inteiro no lugar do
    /// anterior. Um arquivo que não confere é recusado inteiro, sem nenhuma nota gravada.
    /// </summary>
    /// <remarks>
    /// Responde 201 com o lote criado, ou 200 com o lote existente quando o arquivo é o do lote vigente.
    /// O limite do arquivo é de 10 MiB; a recusa de tamanho acontece sem ler o corpo quando o
    /// <c>Content-Length</c> já o excede. Sem <c>Idempotency-Key</c>: reenviar o mesmo arquivo é
    /// idempotente pelo hash dele. O formulário tem os campos <c>arquivo</c>, <c>leiauteCodigo</c> e
    /// <c>leiauteVersao</c>; o nome do arquivo enviado nunca é gravado nem logado.
    /// </remarks>
    [HttpPost(BaseDaEdicao + "/lotes-de-notas")]
    [Consumes("multipart/form-data")]
    [CorpoMultipart(CampoDoArquivo, CamposDeTexto = [CampoDoLeiauteCodigo, CampoDoLeiauteVersao])]
    [DesligaOVinculoDoFormulario]
    [RequestSizeLimit(LimitesDoArquivoDoInep.TamanhoMaximoDaRequisicaoEmBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = LimitesDoArquivoDoInep.TamanhoMaximoDaRequisicaoEmBytes)]
    [VendorMediaType(Resource = "lote-de-notas-do-enem", Versions = [1])]
    [ProducesResponseType(typeof(LoteDeNotasDoEnemDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(LoteDeNotasDoEnemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status415UnsupportedMediaType)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ImportarNotas(
        Guid processoSeletivoId, Guid etapaId, string edicao, CancellationToken cancellationToken)
    {
        if (Request.ContentLength > LimitesDoArquivoDoInep.TamanhoMaximoDaRequisicaoEmBytes)
        {
            return Recusa(RecusasDoArquivoDoInep.GrandeDemais);
        }

        if (!Request.HasFormContentType)
        {
            return Recusa(RecusasDoArquivoDoInep.NaoEhFormulario);
        }

        IFormCollection formulario;
        try
        {
            formulario = await Request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (BadHttpRequestException ex) when (ex.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return Recusa(RecusasDoArquivoDoInep.GrandeDemais);
        }
        catch (InvalidDataException ex)
        {
            // O limite de tamanho do multipart também chega como InvalidDataException; o restante é formulário malformado.
            return ex.Message.Contains("limit", StringComparison.OrdinalIgnoreCase)
                ? Recusa(RecusasDoArquivoDoInep.GrandeDemais)
                : BadRequest(new ProblemDetails { Title = "Formulário malformado", Status = StatusCodes.Status400BadRequest });
        }

        IFormFile? arquivo = formulario.Files.GetFile(CampoDoArquivo);
        if (arquivo is null || arquivo.Length == 0)
        {
            return Recusa(RecusasDoArquivoDoInep.Vazio);
        }

        if (arquivo.Length > LimitesDoArquivoDoInep.TamanhoMaximoEmBytes)
        {
            return Recusa(RecusasDoArquivoDoInep.GrandeDemais);
        }

        if (!TipoDeConteudoAceito(arquivo.ContentType))
        {
            return Recusa(RecusasDoArquivoDoInep.NaoEhTexto);
        }

        byte[] conteudo = new byte[arquivo.Length];
        await using (Stream leitura = arquivo.OpenReadStream())
        {
            await leitura.ReadExactlyAsync(conteudo, cancellationToken).ConfigureAwait(false);
        }

        Result<ImportacaoDeNotasDto> resultado = await _commandBus
            .Send(
                new ImportarNotasDoEnemCommand(
                    processoSeletivoId, etapaId, edicao,
                    formulario[CampoDoLeiauteCodigo].ToString(), formulario[CampoDoLeiauteVersao].ToString(), conteudo),
                cancellationToken)
            .ConfigureAwait(false);
        if (resultado.IsFailure)
        {
            return resultado.ToActionResult(_mapper);
        }

        ImportacaoDeNotasDto importacao = resultado.Value!;
        return importacao.Criado
            ? CreatedAtAction(
                nameof(ObterLote),
                new { processoSeletivoId, etapaId, edicao, loteId = importacao.Lote.Id },
                importacao.Lote)
            : Ok(importacao.Lote);
    }

    /// <summary>Lista os lotes de notas da edição, do mais recente para o mais antigo, marcando o vigente. Sem as ocorrências.</summary>
    [HttpGet(BaseDaEdicao + "/lotes-de-notas")]
    [VendorMediaType(Resource = "lote-de-notas-do-enem", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<LoteDeNotasDoEnemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ListarLotes(
        Guid processoSeletivoId, Guid etapaId, string edicao, CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<LoteDeNotasDoEnemDto>> resultado = await _queryBus
            .Send(new ListarLotesDeNotasQuery(processoSeletivoId, etapaId, edicao), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(_mapper);
    }

    /// <summary>Obtém um lote de notas com tudo o que não virou nota: inscritos sem linha, linhas sem inscrito e conflitos.</summary>
    [HttpGet(BaseDaEdicao + "/lotes-de-notas/{loteId:guid}")]
    [VendorMediaType(Resource = "lote-de-notas-do-enem", Versions = [1])]
    [ProducesResponseType(typeof(LoteDeNotasDoEnemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ObterLote(
        Guid processoSeletivoId, Guid etapaId, string edicao, Guid loteId, CancellationToken cancellationToken)
    {
        Result<LoteDeNotasDoEnemDto> resultado = await _queryBus
            .Send(new ObterLoteDeNotasQuery(processoSeletivoId, etapaId, edicao, loteId), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(_mapper);
    }

    private IActionResult Recusa(Result recusa) => recusa.ToActionResult(_mapper);

    private static bool TipoDeConteudoAceito(string? tipoDeConteudo)
    {
        if (string.IsNullOrWhiteSpace(tipoDeConteudo))
        {
            return true;
        }

        string tipo = tipoDeConteudo.Split(';', 2)[0].Trim();
        return TiposDeConteudoAceitos.Contains(tipo, StringComparer.OrdinalIgnoreCase);
    }
}
