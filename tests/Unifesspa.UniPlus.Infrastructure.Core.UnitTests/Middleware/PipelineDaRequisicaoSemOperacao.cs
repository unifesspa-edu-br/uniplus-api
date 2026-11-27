namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Middleware;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Middleware;

/// <summary>Roda a resposta da requisição sem operação como o roteamento a deixa: endpoint, status e Allow já definidos.</summary>
internal static class PipelineDaRequisicaoSemOperacao
{
    private const string BaseUriDoCatalogo = "https://unifesspa-edu-br.github.io/uniplus-developers/erros/";

    internal static async Task<DefaultHttpContext> ExecutarAsync(int status, Endpoint? endpoint, string? allow = null)
    {
        ServiceCollection servicos = new();
        servicos.AddSingleton<IProblemTypeUriFactory>(new ProblemTypeUriFactory(Options.Create(new ProblemTypeOptions { BaseUri = BaseUriDoCatalogo })));
        ServiceProvider provider = servicos.BuildServiceProvider();
        ApplicationBuilder app = new(provider);
        app.UseRespostaDaRequisicaoSemOperacao();
        app.Run(requisicao => DeixarComoORoteamentoDeixa(requisicao, status, endpoint, allow));
        DefaultHttpContext contexto = new() { RequestServices = provider };
        contexto.Response.Body = new MemoryStream();

        await app.Build()(contexto);
        return contexto;
    }

    private static Task DeixarComoORoteamentoDeixa(HttpContext requisicao, int status, Endpoint? endpoint, string? allow)
    {
        requisicao.SetEndpoint(endpoint);
        requisicao.Response.StatusCode = status;
        if (allow is not null)
        {
            requisicao.Response.Headers[HeaderNames.Allow] = allow;
        }

        return Task.CompletedTask;
    }
}
