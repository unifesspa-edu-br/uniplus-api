namespace Unifesspa.UniPlus.Infrastructure.Core.Middleware;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

internal static class RequisicaoComOperacaoExtensions
{
    /// <summary>
    /// Indica se o roteamento selecionou uma operação da API para a requisição. Rota inexistente ou método não aceito
    /// deixam sem endpoint ou com um endpoint que não é de rota (o do 405).
    /// </summary>
    internal static bool ChegouAUmaOperacao(this HttpContext context) =>
        context.GetEndpoint() is RouteEndpoint;
}
