namespace Unifesspa.UniPlus.Infrastructure.Core.CorpoDaRequisicao;

using Errors;

using Microsoft.AspNetCore.Http;

using Middleware;

/// <summary>
/// A resposta 413 do corpo acima do limite (<c>uniplus.requisicao.corpo_acima_do_limite</c>), escrita pelo middleware do limite,
/// que recusa pelo tamanho declarado, e pela tradução da exceção que o servidor lança ao ler além do limite. Informa só o
/// limite: nada do corpo nem o tamanho declarado.
/// </summary>
internal static class RespostaDoCorpoAcimaDoLimite
{
    internal static Task EscreverAsync(HttpContext context, long limiteEmBytes, IProblemTypeUriFactory problemTypeUriFactory) =>
        ProblemaNaResposta.EscreverAsync(
            context,
            problemTypeUriFactory,
            ErrosDaFronteiraHttp.CorpoAcimaDoLimite,
            $"O corpo da requisição passa do limite de {limiteEmBytes} bytes aceito por esta operação. Reduza o conteúdo enviado.");
}
