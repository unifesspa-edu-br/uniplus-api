namespace Unifesspa.UniPlus.Infrastructure.Core.Middleware;

using Errors;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// A resposta de erro da requisição que não chegou a uma operação da API: 404 com <c>uniplus.requisicao.caminho_inexistente</c>
/// (a rota que não existe e o identificador de rota fora do formato que a operação aceita, que o roteamento descarta do mesmo
/// modo) e 405 com <c>uniplus.requisicao.metodo_nao_aceito</c> (a rota existe, mas não aceita o método).
/// </summary>
/// <remarks>
/// Só escreve quando nenhuma operação foi selecionada. O 404 de registro inexistente, em que a operação foi selecionada, já sai
/// com o código do assunto e não passa por aqui; o 401/403 também ficam como estão. A resposta não é limpa antes da escrita,
/// para preservar o cabeçalho <c>Allow</c> que o roteamento grava no 405. Nem o caminho nem o método pedidos vão à resposta.
/// </remarks>
public static class RespostaDaRequisicaoSemOperacao
{
    private const string DetalheDoCaminhoInexistente =
        "Nenhuma operação da API atende a este caminho. Confira o caminho e o formato dos identificadores na referência da API.";

    private const string DetalheDoMetodoNaoAceito =
        "O caminho existe, mas não aceita o método usado. O cabeçalho Allow da resposta informa os métodos aceitos.";

    public static IApplicationBuilder UseRespostaDaRequisicaoSemOperacao(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseStatusCodePages(static contexto => EscreverSeNaoChegouAUmaOperacao(contexto.HttpContext));
    }

    private static Task EscreverSeNaoChegouAUmaOperacao(HttpContext context)
    {
        if (context.ChegouAUmaOperacao())
        {
            return Task.CompletedTask;
        }

        (DomainErrorMapping, string)? resposta = context.Response.StatusCode switch
        {
            StatusCodes.Status404NotFound => (ErrosDaFronteiraHttp.CaminhoInexistente, DetalheDoCaminhoInexistente),
            StatusCodes.Status405MethodNotAllowed => (ErrosDaFronteiraHttp.MetodoNaoAceito, DetalheDoMetodoNaoAceito),
            _ => null,
        };

        return resposta is var (definicao, detalhe)
            ? ProblemaNaResposta.EscreverAsync(context, context.RequestServices.GetRequiredService<IProblemTypeUriFactory>(), definicao, detalhe)
            : Task.CompletedTask;
    }
}
