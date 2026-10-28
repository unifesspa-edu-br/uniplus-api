namespace Unifesspa.UniPlus.Infrastructure.Core.Authorization;

using System.Diagnostics;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>Endpoints de consulta do catálogo de permissões (ADR-0151).</summary>
public static class AutorizacaoEndpointsExtensions
{
    /// <summary>Media type versionado do catálogo (ADR-0028).</summary>
    public const string MediaTypeCatalogo = "application/vnd.uniplus.permissao-catalogo.v1+json";

    /// <summary>Media type versionado das permissões efetivas (ADR-0028).</summary>
    public const string MediaTypePermissoesEfetivas = "application/vnd.uniplus.permissao-efetiva.v1+json";

    /// <summary>
    /// Mapeia <c>GET /api/autorizacao/permissoes</c> (catálogo, para quem administra
    /// concessões) e <c>GET /api/autorizacao/permissoes/efetivas</c> (permissões do
    /// usuário logado, para o frontend montar menu e ações).
    /// </summary>
    public static IEndpointRouteBuilder MapSharedAutorizacaoEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder grupo = endpoints.MapGroup("/api/autorizacao/permissoes")
            .WithTags("Autorização")
            .RequireAuthorization();

        grupo.MapGet("/", ConsultarCatalogoAsync)
            .WithName("ConsultarCatalogoDePermissoes")
            .WithSummary("Lista o catálogo de permissões")
            .WithDescription("Permissões da plataforma agrupadas por módulo e recurso, com código, nome, descrição e sensibilidade. Exige a permissão autorizacao:permissoes:consultar-catalogo.")
            .Produces<IReadOnlyList<ModuloDoCatalogoResponse>>(StatusCodes.Status200OK, MediaTypeCatalogo)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status406NotAcceptable);

        grupo.MapGet("/efetivas", ConsultarEfetivasAsync)
            .WithName("ConsultarPermissoesEfetivas")
            .WithSummary("Lista as permissões efetivas do usuário logado")
            .WithDescription("Código e nome de cada permissão que o token concede. Informa o que pode aparecer na interface; a decisão de acesso continua sendo tomada em cada requisição.")
            .Produces<IReadOnlyList<PermissaoEfetivaResponse>>(StatusCodes.Status200OK, MediaTypePermissoesEfetivas)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status406NotAcceptable);

        return endpoints;
    }

    private static readonly int[] VersoesServidas = [1];

    private static async Task<IResult> ConsultarCatalogoAsync(
        HttpContext http,
        IVerificadorDeAcesso acesso,
        CatalogoDaAplicacao catalogo,
        CancellationToken cancellationToken)
    {
        if (NaoAceita(http, MediaTypeCatalogo))
        {
            return NaoAceitavel(http, "permissao-catalogo");
        }

        DefinicaoDePermissao exigida = CatalogoDePermissoes<PermissaoAutorizacao>.Obter(PermissaoAutorizacao.ConsultarCatalogo);
        ResultadoDoAcesso resultado = await acesso
            .VerificarAsync(exigida.Requisito, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (resultado != ResultadoDoAcesso.Permitido)
        {
            return resultado == ResultadoDoAcesso.IdentidadeIncompleta ? Results.Challenge() : Results.Forbid();
        }

        IReadOnlyList<ModuloDoCatalogoResponse> corpo =
        [
            .. catalogo.Definicoes
                .GroupBy(static d => d.Modulo)
                .Select(static modulo => new ModuloDoCatalogoResponse(
                    modulo.Key,
                    [.. modulo
                        .GroupBy(static d => d.Recurso)
                        .Select(static recurso => new RecursoDoCatalogoResponse(
                            recurso.Key,
                            [.. recurso.Select(static d => new PermissaoDoCatalogoResponse(
                                d.Codigo,
                                d.Nome,
                                d.Descricao,
                                ValoresCanonicos.De(d.Requisito.Sensibilidade)))]))])),
        ];

        http.Response.Headers.Append(HeaderNames.Vary, "Accept");

        return Results.Json(corpo, contentType: MediaTypeCatalogo);
    }

    private static async Task<IResult> ConsultarEfetivasAsync(
        HttpContext http,
        IPermissoesEfetivas efetivas,
        CancellationToken cancellationToken)
    {
        if (NaoAceita(http, MediaTypePermissoesEfetivas))
        {
            return NaoAceitavel(http, "permissao-efetiva");
        }

        IReadOnlyList<DefinicaoDePermissao>? permissoes = await efetivas
            .ObterAsync(cancellationToken)
            .ConfigureAwait(false);

        if (permissoes is null)
        {
            return Results.Challenge();
        }

        http.Response.Headers.Append(HeaderNames.Vary, "Accept");

        return Results.Json(
            permissoes.Select(static d => new PermissaoEfetivaResponse(d.Codigo, d.Nome)).ToArray(),
            contentType: MediaTypePermissoesEfetivas);
    }

    // Accept ausente, */*, application/json e o próprio vendor aceitam a versão única;
    // qualquer outro pedido é 406 (ADR-0028).
    private static bool NaoAceita(HttpContext http, string mediaType)
    {
        IList<MediaTypeHeaderValue>? aceitos = http.Request.GetTypedHeaders().Accept;
        if (aceitos is null || aceitos.Count == 0)
        {
            return false;
        }

        // q=0 exclui a representação (RFC 9110 §12.5.1) e prevalece sobre um curinga
        // que a aceitaria: a exclusão explícita é a faixa mais específica.
        if (aceitos.Any(a => a.Quality is 0 && Alcanca(a, mediaType)))
        {
            return true;
        }

        return !aceitos.Any(a => a.Quality is not 0 && Alcanca(a, mediaType));
    }

    private static bool Alcanca(MediaTypeHeaderValue faixa, string mediaType)
        => faixa.MediaType.Equals("*/*", StringComparison.OrdinalIgnoreCase)
            || faixa.MediaType.Equals("application/*", StringComparison.OrdinalIgnoreCase)
            || faixa.MediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase)
            || faixa.MediaType.Equals(mediaType, StringComparison.OrdinalIgnoreCase);

    // Mesmo contrato do 406 de VendorMediaTypeAttribute (ADR-0028): código estável e a
    // lista de versões servidas, para o cliente descobrir o que pode pedir.
    private static IResult NaoAceitavel(HttpContext http, string recurso)
    {
        const string codigo = "uniplus.contract.versao_nao_suportada";

        IProblemTypeUriFactory tipos = http.RequestServices.GetRequiredService<IProblemTypeUriFactory>();
        ProblemDetails problema = new()
        {
            Status = StatusCodes.Status406NotAcceptable,
            Type = tipos.Build(codigo),
            Title = "Versão de mídia não suportada",
            Detail = $"Nenhuma versão suportada pelo recurso '{recurso}' foi aceita pelo cliente.",
            Instance = $"urn:uuid:{Guid.CreateVersion7()}",
        };

        problema.Extensions["code"] = codigo;
        problema.Extensions["available_versions"] = VersoesServidas;
        problema.Extensions["traceId"] = Activity.Current?.TraceId.ToHexString() ?? Guid.CreateVersion7().ToString("N");

        return Results.Problem(problema);
    }
}

/// <summary>Permissões de um módulo no catálogo.</summary>
/// <param name="Modulo">Módulo.</param>
/// <param name="Recursos">Recursos do módulo.</param>
public sealed record ModuloDoCatalogoResponse(string Modulo, IReadOnlyList<RecursoDoCatalogoResponse> Recursos);

/// <summary>Permissões de um recurso no catálogo.</summary>
/// <param name="Recurso">Recurso.</param>
/// <param name="Permissoes">Permissões do recurso.</param>
public sealed record RecursoDoCatalogoResponse(string Recurso, IReadOnlyList<PermissaoDoCatalogoResponse> Permissoes);

/// <summary>Uma permissão do catálogo.</summary>
/// <param name="Codigo">Código da permissão.</param>
/// <param name="Nome">Nome legível.</param>
/// <param name="Descricao">Descrição do que autoriza.</param>
/// <param name="Sensibilidade">Sensibilidade do dado (publica, interna, pessoal, sensivel).</param>
public sealed record PermissaoDoCatalogoResponse(string Codigo, string Nome, string Descricao, string Sensibilidade);

/// <summary>Uma permissão que o usuário logado possui.</summary>
/// <param name="Codigo">Código da permissão.</param>
/// <param name="Nome">Nome legível.</param>
public sealed record PermissaoEfetivaResponse(string Codigo, string Nome);
