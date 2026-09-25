namespace Unifesspa.UniPlus.Portal.API.Controllers;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

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
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    [EmiteHeader("Retry-After", DescricaoDoRetryAfter, Status = "503", Inteiro = true)]
    public async Task<IActionResult> ObterCertamePublico(string identificador, CancellationToken cancellationToken)
    {
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

        // Linha do tempo degradada é estado transitório e não é guardada em camada nenhuma
        // (ADR-0131, Emenda 1); a completa é servida com revalidação obrigatória.
        Response.Headers.CacheControl = pagina.Degradacao is null ? "no-cache" : "no-store";

        return Ok(CertamePublicoResponse.De(identificador, pagina));
    }
}
