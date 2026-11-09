namespace Unifesspa.UniPlus.Autorizacao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;

internal static class MapeamentosComuns
{
    /// <summary>
    /// Mesmo código do conflito que o GlobalExceptionMiddleware emite para endpoints sem idempotência: a corrida é a
    /// mesma, só muda quem a traduz. Retentável, porque a requisição idêntica repetida depois da corrida lê o estado novo.
    /// </summary>
    public static KeyValuePair<string, DomainErrorMapping> Concorrencia(string codigo) =>
        new(codigo, new DomainErrorMapping(StatusCodes.Status409Conflict, "uniplus.concorrencia.conflito", "Conflito de concorrência", RetryableConflict: true));
}
