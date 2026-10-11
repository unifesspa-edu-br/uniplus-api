namespace Unifesspa.UniPlus.Portal.API.Controllers;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Infrastructure.Core.OpenApi;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Portal.Application.Certames;
using Unifesspa.UniPlus.Portal.Infrastructure.Origens;

/// <summary>
/// Página pública do certame no Portal do Candidato — o detalhe publicado por Seleção composto com
/// a linha do tempo de atos de Publicações (ADR-0131).
/// </summary>
/// <remarks>
/// Leitura anônima. A Portal não reaplica o critério de visibilidade: o "não encontrado" de
/// Seleção é repassado como tal.
/// </remarks>
[ApiController]
[AllowAnonymous]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public; sem isso o MVC ignora a classe e nenhum endpoint é registrado.")]
public sealed class CertamePublicoController : ControllerBase
{
    private const string DescricaoDoSelo =
        "Selo da página composta. Devolva-o no If-None-Match da próxima leitura: a resposta é de "
        + "revalidação obrigatória (Cache-Control: no-cache), e o selo permite receber 304 em vez da "
        + "página inteira. Ausente quando a linha do tempo está incompleta, porque essa resposta não "
        + "é guardada.";

    private static readonly JsonSerializerOptions OpcoesDoSelo = new(JsonSerializerDefaults.Web);

    private const string DescricaoDoRetryAfter =
        "Em quantos segundos vale tentar de novo. Presente quando os dados do certame estão "
        + "temporariamente indisponíveis.";

    private readonly ComposicaoDaPaginaDoCertame _composicao;
    private readonly IDomainErrorMapper _mapper;
    private readonly OrigensOptions _origens;

    public CertamePublicoController(
        ComposicaoDaPaginaDoCertame composicao,
        IDomainErrorMapper mapper,
        IOptions<OrigensOptions> origens)
    {
        ArgumentNullException.ThrowIfNull(composicao);
        ArgumentNullException.ThrowIfNull(mapper);
        ArgumentNullException.ThrowIfNull(origens);

        _composicao = composicao;
        _mapper = mapper;
        _origens = origens.Value;
    }

    /// <summary>
    /// Detalhe público do certame e sua linha do tempo de publicações, pelo identificador legível.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>404</c> cobre, com a mesma resposta, o processo inexistente, o rascunho e a abertura sem
    /// ato registrado — a resposta não distingue os casos.
    /// </para>
    /// <para>
    /// <c>503</c> quando os dados do certame estão indisponíveis na origem: a Portal não serve
    /// conteúdo vencido no lugar, e indica em <c>Retry-After</c> quando tentar de novo.
    /// </para>
    /// <para>
    /// Quando só a linha do tempo não pode ser montada por completo, os dados do certame saem
    /// assim mesmo, com <c>aviso</c> e sem guarda em cache.
    /// </para>
    /// </remarks>
    [HttpGet("certames/{identificador}")]
    [VendorMediaType(Resource = "certame-publico", Versions = [1])]
    [ProducesResponseType(typeof(CertamePublicoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [EmiteETag(Descricao = DescricaoDoSelo)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    [EmiteHeader("Retry-After", DescricaoDoRetryAfter, Status = "503", Inteiro = true)]
    public async Task<IActionResult> ObterCertamePublico(
        string? identificador,
        [FromHeader(Name = "If-None-Match")] string? ifNoneMatch,
        CancellationToken cancellationToken)
    {
        // Anulável de propósito: o binder transforma um segmento só de espaços em nulo, e um
        // parâmetro obrigatório faria essa entrada sair como 400 de validação — o endereço fora do
        // formato tem de dar o mesmo "não encontrado" dos demais casos.
        identificador ??= string.Empty;

        Result<PaginaDoCertame> resultado = await _composicao
            .ComporPorIdentificadorAsync(identificador, cancellationToken)
            .ConfigureAwait(false);

        if (resultado.IsFailure)
        {
            if (resultado.Error!.Code == PaginaDoCertameErrorCodes.OrigemIndisponivel)
            {
                // Estado transitório: nada dele pode ser guardado, e o cliente recebe quando voltar.
                Response.Headers.CacheControl = "no-store";
                Response.Headers.RetryAfter =
                    _origens.ReencaminharAposEmSegundos.ToString(CultureInfo.InvariantCulture);
            }
            else
            {
                // O mesmo endereço passa a existir quando o certame é publicado: revalidação
                // obrigatória, para que nenhum cache atribua frescor heurístico ao 404.
                Response.Headers.CacheControl = "no-cache";
            }

            return resultado.ToActionResult(_mapper);
        }

        PaginaDoCertame pagina = resultado.Value!;
        CertamePublicoResponse resposta = CertamePublicoResponse.De(identificador, pagina);

        // Linha do tempo degradada é estado transitório e não é guardada em camada nenhuma
        // (ADR-0131, Emenda 1); a completa é servida com revalidação obrigatória.
        if (pagina.Degradacao is not null)
        {
            Response.Headers.CacheControl = "no-store";
            return Ok(resposta);
        }

        Response.Headers.CacheControl = "no-cache";

        // A página é composta de duas origens, e nenhum selo de origem sozinho muda quando a outra
        // muda: o selo é o do conteúdo servido, calculado a cada leitura sem guardar a composição.
        string selo = SeloDe(resposta);
        Response.Headers.ETag = selo;

        return SeloDeEntidade.IfNoneMatchCoincide(ifNoneMatch, selo)
            ? StatusCode(StatusCodes.Status304NotModified)
            : Ok(resposta);
    }

    private static string SeloDe(CertamePublicoResponse resposta)
    {
        byte[] conteudo = JsonSerializer.SerializeToUtf8Bytes(resposta, OpcoesDoSelo);
        return $"\"{Convert.ToHexStringLower(SHA256.HashData(conteudo))}\"";
    }
}
