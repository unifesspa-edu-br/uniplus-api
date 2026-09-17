namespace Unifesspa.UniPlus.Selecao.API.Controllers;

using System.Diagnostics.CodeAnalysis;

using Application.DTOs;
using Application.Queries.ProcessosSeletivos;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Leitura pública do certame publicado — o contrato que o portal do candidato e qualquer outro
/// consumidor leem para montar a página de um processo seletivo.
/// </summary>
/// <remarks>
/// Sem <c>[Authorize]</c> de classe e sem escrita: este controlador é só leitura anônima. A
/// configuração do processo continua sendo escrita pelas rotas administrativas, e a leitura
/// administrativa do processo em rascunho continua restrita — esta rota não a afrouxa, serve outro
/// recurso, projetado da versão congelada.
/// </remarks>
[ApiController]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public; sem isso o MVC ignora a classe e nenhum endpoint é registrado.")]
public sealed class CertamePublicadoController : ControllerBase
{
    private readonly IQueryBus _queryBus;
    private readonly IDomainErrorMapper _mapper;

    public CertamePublicadoController(IQueryBus queryBus, IDomainErrorMapper mapper)
    {
        _queryBus = queryBus;
        _mapper = mapper;
    }

    /// <summary>
    /// Certame publicado, projetado da versão de configuração vigente e visível apenas quando o ato
    /// normativo que a criou está registrado.
    /// </summary>
    /// <remarks>
    /// <c>404</c> cobre, com a mesma resposta, o processo inexistente, o processo em rascunho, o
    /// processo sem versão vigente e aquele cujo ato ainda não foi registrado — inclusive quando o
    /// registro foi recusado. A resposta não distingue os casos de propósito: para um chamador
    /// anônimo, distinguir seria responder "esse identificador é um rascunho?".
    /// </remarks>
    [HttpGet("processos-seletivos/{id:guid}/certame")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "certame", Versions = [1])]
    [ProducesResponseType(typeof(CertamePublicadoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ObterCertamePublicado(
        Guid id,
        [FromHeader(Name = "If-None-Match")] string? ifNoneMatch,
        CancellationToken cancellationToken)
    {
        Result<CertamePublicadoDto> resultado = await _queryBus
            .Send(new ObterCertamePublicadoQuery(id), cancellationToken)
            .ConfigureAwait(false);

        if (resultado.IsFailure)
        {
            return resultado.ToActionResult(_mapper);
        }

        CertamePublicadoDto certame = resultado.Value!;
        string etag = $"\"{certame.VersaoProjecao}:{certame.HashConfiguracao}\"";

        // Revalidação OBRIGATÓRIA, não cache proibido: o cliente pode guardar, mas precisa
        // confirmar antes de usar. O endereço da página não muda quando o certame é retificado, de
        // modo que uma representação já guardada na borda ou no navegador seria servida sem que
        // ninguém consultasse a origem — e é na confirmação que o selo faz o seu trabalho.
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.ETag = etag;

        return EtagCoincide(ifNoneMatch, etag) ? StatusCode(StatusCodes.Status304NotModified) : Ok(certame);
    }

    /// <summary>
    /// Compara o selo recebido com o corrente. Aceita lista separada por vírgula e o curinga, como
    /// a especificação de requisições condicionais manda, e compara byte a byte — o selo é opaco
    /// para o cliente, e normalizá-lo abriria espaço para dois selos distintos passarem por iguais.
    /// </summary>
    private static bool EtagCoincide(string? ifNoneMatch, string etagAtual)
    {
        if (string.IsNullOrWhiteSpace(ifNoneMatch))
        {
            return false;
        }

        foreach (string candidato in ifNoneMatch.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (candidato == "*" || string.Equals(candidato, etagAtual, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
