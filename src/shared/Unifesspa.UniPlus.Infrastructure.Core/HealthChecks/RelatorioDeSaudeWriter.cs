namespace Unifesspa.UniPlus.Infrastructure.Core.HealthChecks;

using System.Text.Json;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// Escreve o health completo em JSON com o estado agregado e o de cada verificação. Só nome e estado saem na
/// resposta, pública: descrição e exceção ficam no log, para não expor detalhe de infraestrutura.
/// </summary>
internal static class RelatorioDeSaudeWriter
{
    public static Task Escrever(HttpContext contexto, HealthReport relatorio)
    {
        contexto.Response.ContentType = "application/json; charset=utf-8";

        return JsonSerializer.SerializeAsync(contexto.Response.Body, new
        {
            status = relatorio.Status.ToString(),
            verificacoes = relatorio.Entries.ToDictionary(
                static e => e.Key,
                static e => new { status = e.Value.Status.ToString() }),
        }, cancellationToken: contexto.RequestAborted);
    }
}
