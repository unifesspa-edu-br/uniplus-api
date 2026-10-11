namespace Unifesspa.UniPlus.Portal.UnitTests.Origens;

using System.Collections.Concurrent;
using System.Net;
using System.Text;

/// <summary>
/// Substitui só a rede: registra cada requisição e responde conforme o roteiro do teste.
/// </summary>
internal sealed class RedeSimulada(Func<HttpRequestMessage, int, HttpResponseMessage> responder) : HttpMessageHandler
{
    private int _chamadas;

    public ConcurrentQueue<HttpRequestMessage> Requisicoes { get; } = new();

    public int Chamadas => _chamadas;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        int numero = Interlocked.Increment(ref _chamadas);
        Requisicoes.Enqueue(request);

        // A rede real sempre associa a resposta à requisição; o cliente depende disso para montar
        // o erro de uma resposta sem sucesso.
        HttpResponseMessage resposta = responder(request, numero);
        resposta.RequestMessage = request;

        return Task.FromResult(resposta);
    }

    public static HttpResponseMessage Json(string corpo, string mediaType, HttpStatusCode status = HttpStatusCode.OK)
    {
        HttpResponseMessage resposta = new(status)
        {
            Content = new StringContent(corpo, Encoding.UTF8),
        };
        resposta.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mediaType)
        {
            CharSet = "utf-8",
        };
        return resposta;
    }

    public static HttpResponseMessage Status(HttpStatusCode status) => new(status);
}
