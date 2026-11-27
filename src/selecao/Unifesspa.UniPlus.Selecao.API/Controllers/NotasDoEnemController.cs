namespace Unifesspa.UniPlus.Selecao.API.Controllers;

using System.Diagnostics.CodeAnalysis;

using Application.Commands.NotasDoEnem;
using Application.DTOs.NotasDoEnem;
using Application.Queries.NotasDoEnem;

using Contracts.Requests;

using Domain.Leiautes;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Infrastructure.Core.Authorization;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Infrastructure.Core.OpenApi;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.API.Authorization;
using Unifesspa.UniPlus.Selecao.API.Http;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;

/// <summary>
/// Envio dos CPFs da edição do ENEM ao INEP e importação das notas que o INEP devolve. Cada operação
/// exige a permissão própria na unidade dona do processo (gerar o envio, importar e consultar as notas); a lista de
/// leiautes, que não é de um processo, aceita a permissão institucional ou a de consulta numa unidade.
/// </summary>
[ApiController]
[EmiteEsperaEsgotadaPelaTrava]
[Authorize]
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
    [EndpointName("ListarLeiautesDeArquivoDoInep")]
    [ExigePermissao<PermissaoSelecao>(
        PermissaoSelecao.LeiautesInepConsultar,
        OuEmAlgumaUnidade = PermissaoSelecao.LeiautesInepConsultarNaUnidade,
        ResourceTipo = ResourceTiposSelecao.LeiauteInep)]
    [VendorMediaType(Resource = "leiaute-de-arquivo-do-inep", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<LeiauteDoInepDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ListarLeiautes(
        [FromQuery(Name = "tipo"), AceitaCodigosDe<TipoDeLeiauteDoInep>] string? tipo,
        CancellationToken cancellationToken)
    {
        Result<TipoDeLeiauteDoInep?> filtro = FiltroDeTipoDosLeiautesDoInep.Ler(tipo);
        if (filtro.IsFailure)
        {
            return filtro.ToActionResult(_mapper);
        }

        IReadOnlyList<LeiauteDoInepDto> leiautes = await _queryBus
            .Send(new ListarLeiautesDoInepQuery(filtro.Value), cancellationToken)
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
    [EndpointName("GerarEnvioAoInep")]
    [ExigePermissaoNoProcesso(PermissaoSelecao.EnviosInepGerar, ParametroDaRota = RotaDoProcesso.ParametroAninhado)]
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
    [EndpointName("ListarEnviosAoInep")]
    [ExigePermissaoNoProcesso(PermissaoSelecao.EnviosInepGerar, ParametroDaRota = RotaDoProcesso.ParametroAninhado)]
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
    /// O arquivo tem no máximo 10 MiB e a requisição inteira, com os campos do formulário, 64 KiB a mais: acima disso a recusa é
    /// do limite de corpo da operação (413, <c>uniplus.requisicao.corpo_acima_do_limite</c>). Com o tamanho declarado acima do limite, ela vem antes de
    /// verificar o acesso e de ler o conteúdo; sem tamanho declarado (conteúdo em partes), a leitura é interrompida ao passar do
    /// limite, e a verificação de acesso pode recusar antes disso. O arquivo acima dos 10 MiB dentro dessa folga é recusado pelo tamanho do arquivo. Sem <c>Idempotency-Key</c>: reenviar o mesmo arquivo é
    /// idempotente pelo hash dele. O formulário tem os campos <c>arquivo</c>, <c>leiauteCodigo</c> e
    /// <c>leiauteVersao</c>; o nome do arquivo enviado nunca é gravado nem logado.
    /// </remarks>
    [HttpPost(BaseDaEdicao + "/lotes-de-notas")]
    [EndpointName("ImportarNotasDoEnem")]
    [ExigePermissaoNoProcesso(PermissaoSelecao.NotasEnemImportar, ParametroDaRota = RotaDoProcesso.ParametroAninhado)]
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
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge,
        Description = "A requisição passa de 10 MiB mais 64 KiB de folga para os campos do formulário (uniplus.requisicao.corpo_acima_do_limite), ou o arquivo passa de 10 MiB (uniplus.selecao.notas_do_enem.arquivo_grande_demais).")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status415UnsupportedMediaType)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ImportarNotas(
        Guid processoSeletivoId, Guid etapaId, string edicao, CancellationToken cancellationToken)
    {
        (IFormCollection? formulario, IActionResult? recusa) = await LerFormularioDoRetornoAsync(cancellationToken).ConfigureAwait(false);
        if (recusa is not null)
        {
            return recusa;
        }
        IFormFile? arquivo = formulario!.Files.GetFile(CampoDoArquivo);
        recusa = ValidarArquivoDoRetorno(arquivo);
        if (recusa is not null)
        {
            return recusa;
        }

        byte[] conteudo = await LerArquivoDoRetornoAsync(arquivo!, cancellationToken).ConfigureAwait(false);
        return await ImportarRetornoAsync(processoSeletivoId, etapaId, edicao, formulario, conteudo, cancellationToken).ConfigureAwait(false);
    }

    private async Task<(IFormCollection? Formulario, IActionResult? Recusa)> LerFormularioDoRetornoAsync(CancellationToken cancellationToken)
    {
        if (!Request.HasFormContentType)
        {
            return (null, Recusa(RecusasDoArquivoDoInep.NaoEhFormulario));
        }

        return await LerFormularioAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<(IFormCollection? Formulario, IActionResult? Recusa)> LerFormularioAsync(CancellationToken cancellationToken)
    {
        try
        {
            return (await Request.ReadFormAsync(cancellationToken).ConfigureAwait(false), null);
        }
        catch (InvalidDataException ex)
        {
            return (null, RecusaDoFormulario(ex));
        }
    }

    private IActionResult RecusaDoFormulario(InvalidDataException excecao) =>
        excecao.Message.Contains("limit", StringComparison.OrdinalIgnoreCase)
            ? Recusa(RecusasDoArquivoDoInep.GrandeDemais)
            : BadRequest(new ProblemDetails { Title = "Formulário malformado", Status = StatusCodes.Status400BadRequest });

    private IActionResult? ValidarArquivoDoRetorno(IFormFile? arquivo)
    {
        if (arquivo is null || arquivo.Length == 0)
        {
            return Recusa(RecusasDoArquivoDoInep.Vazio);
        }

        if (arquivo.Length > LimitesDoArquivoDoInep.TamanhoMaximoEmBytes)
        {
            return Recusa(RecusasDoArquivoDoInep.GrandeDemais);
        }

        return TipoDeConteudoAceito(arquivo.ContentType) ? null : Recusa(RecusasDoArquivoDoInep.NaoEhTexto);
    }

    private static async Task<byte[]> LerArquivoDoRetornoAsync(IFormFile arquivo, CancellationToken cancellationToken)
    {
        byte[] conteudo = new byte[arquivo.Length];
        await using Stream leitura = arquivo.OpenReadStream();
        await leitura.ReadExactlyAsync(conteudo, cancellationToken).ConfigureAwait(false);
        return conteudo;
    }

    private async Task<IActionResult> ImportarRetornoAsync(
        Guid processoSeletivoId, Guid etapaId, string edicao, IFormCollection formulario, byte[] conteudo, CancellationToken cancellationToken)
    {
        Result<ImportacaoDeNotasDto> resultado = await _commandBus.Send(
            new ImportarNotasDoEnemCommand(processoSeletivoId, etapaId, edicao,
                formulario[CampoDoLeiauteCodigo].ToString(), formulario[CampoDoLeiauteVersao].ToString(), conteudo),
            cancellationToken).ConfigureAwait(false);
        return resultado.IsFailure
            ? resultado.ToActionResult(_mapper)
            : RespostaDaImportacao(processoSeletivoId, etapaId, edicao, resultado.Value!);
    }

    private IActionResult RespostaDaImportacao(Guid processoSeletivoId, Guid etapaId, string edicao, ImportacaoDeNotasDto importacao) =>
        importacao.Criado
            ? CreatedAtAction(nameof(ObterLote), new { processoSeletivoId, etapaId, edicao, loteId = importacao.Lote.Id }, importacao.Lote)
            : Ok(importacao.Lote);

    /// <summary>Lista os lotes de notas da edição, do mais recente para o mais antigo, marcando o vigente. Sem as ocorrências.</summary>
    [HttpGet(BaseDaEdicao + "/lotes-de-notas")]
    [EndpointName("ListarImportacoesDeNotasDoEnem")]
    [ExigePermissaoNoProcesso(PermissaoSelecao.NotasEnemImportar, ParametroDaRota = RotaDoProcesso.ParametroAninhado)]
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
    [EndpointName("ObterImportacaoDeNotasDoEnem")]
    [ExigePermissaoNoProcesso(PermissaoSelecao.NotasEnemImportar, ParametroDaRota = RotaDoProcesso.ParametroAninhado)]
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
