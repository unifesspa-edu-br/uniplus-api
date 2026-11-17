namespace Unifesspa.UniPlus.Host.IntegrationTests;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Host.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// O <c>traceId</c> da recusa é o trace-id do contexto W3C da requisição (ADR-0023): quem envia
/// <c>traceparent</c> recebe no corpo o mesmo trace-id, e a resposta não repete o cabeçalho.
/// </summary>
[Collection(MonolitoHostCollection.Name)]
public sealed class TraceIdDaRecusaTests(MonolitoPostgresFixture fixture)
{
    private const string TraceIdEnviado = "4bf92f3577b34da6a3ce929d0e0e4736";
    private const string Traceparent = $"00-{TraceIdEnviado}-00f067aa0ba902b7-01";

    [Fact(DisplayName = "A recusa devolve no traceId o trace-id do traceparent recebido, sem traceparent na resposta")]
    public async Task Recusa_DevolveOTraceIdDoTraceparentRecebido()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        using HttpRequestMessage pedido = new(
            HttpMethod.Get, new Uri($"/api/publicacoes/tipos-ato/{Guid.CreateVersion7()}", UriKind.Relative));
        pedido.Headers.Add("traceparent", Traceparent);

        HttpResponseMessage resposta = await client.SendAsync(pedido);

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using JsonDocument corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        corpo.RootElement.GetProperty("traceId").GetString().Should().Be(TraceIdEnviado);
        resposta.Headers.Contains("traceparent").Should().BeFalse();
    }
}
