namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Messaging.SchemaRegistry;

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// HttpMessageHandler em memória que devolve respostas pré-programadas e contabiliza
/// requests. Substitui IHttpClientFactory/Apicurio real em testes do
/// OAuthBearerAuthenticationHeaderValueProvider.
/// </summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> responses = new();

    public int CallCount { get; private set; }

    public List<HttpRequestMessage> ReceivedRequests { get; } = [];

    public void EnqueueResponse(HttpStatusCode statusCode, string content = "")
        => responses.Enqueue(_ => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content),
        });

    public void EnqueueResponse(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        => responses.Enqueue(responseFactory);

    public void EnqueueException<T>()
        where T : Exception, new()
        => responses.Enqueue(_ => throw new T());

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        CallCount++;
        ReceivedRequests.Add(request);

        if (responses.Count == 0)
        {
            throw new InvalidOperationException(
                $"StubHttpMessageHandler recebeu request #{CallCount} mas a fila de respostas está vazia.");
        }

        Func<HttpRequestMessage, HttpResponseMessage> factory = responses.Dequeue();
        return Task.FromResult(factory(request));
    }
}
