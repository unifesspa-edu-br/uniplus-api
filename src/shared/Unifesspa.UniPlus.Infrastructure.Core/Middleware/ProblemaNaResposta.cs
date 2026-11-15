namespace Unifesspa.UniPlus.Infrastructure.Core.Middleware;

using System.Diagnostics;
using System.Text.Json;

using Errors;

using Microsoft.AspNetCore.Http;

/// <summary>Escreve na resposta o <c>application/problem+json</c> (RFC 9457) do formato único da plataforma.</summary>
internal static class ProblemaNaResposta
{
    internal static readonly JsonSerializerOptions OpcoesJson = new(JsonSerializerDefaults.Web);

    internal static async Task EscreverAsync(
        HttpContext context,
        IProblemTypeUriFactory problemTypeUriFactory,
        DomainErrorMapping definicao,
        string detail)
    {
        context.Response.StatusCode = definicao.Status;

        await context.Response
            .WriteAsJsonAsync(Corpo(problemTypeUriFactory, definicao, detail), OpcoesJson, contentType: "application/problem+json")
            .ConfigureAwait(false);
    }

    private static Dictionary<string, object?> Corpo(IProblemTypeUriFactory problemTypeUriFactory, DomainErrorMapping definicao, string detail) =>
        new()
        {
            ["type"] = problemTypeUriFactory.Build(definicao.Code),
            ["title"] = definicao.Title,
            ["status"] = definicao.Status,
            ["detail"] = detail,
            ["instance"] = $"urn:uuid:{Guid.CreateVersion7()}",
            ["code"] = definicao.Code,
            ["traceId"] = Activity.Current?.TraceId.ToHexString() ?? Guid.CreateVersion7().ToString("N"),
        };
}
