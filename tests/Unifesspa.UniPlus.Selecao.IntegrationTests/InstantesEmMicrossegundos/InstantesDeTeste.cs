namespace Unifesspa.UniPlus.Selecao.IntegrationTests.InstantesEmMicrossegundos;

using System.Net.Http.Headers;
using System.Net.Http.Json;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

/// <summary>Os instantes e o pedido de administrador que os testes de microssegundos compartilham.</summary>
internal static class InstantesDeTeste
{
    /// <summary>Um instante exato em microssegundos: a grade em que o Postgres guarda e o relógio de produção devolve.</summary>
    public static DateTimeOffset EmMicrossegundos => new DateTimeOffset(2027, 3, 10, 12, 0, 0, TimeSpan.Zero).AddTicks(1_234_560);

    /// <summary>O mesmo instante com 7 ticks além do microssegundo, que um relógio sem truncar entrega.</summary>
    public static DateTimeOffset ComResiduoDeTicks => EmMicrossegundos.AddTicks(7);

    public static async Task<HttpResponseMessage> EnviarComoAdministradorAsync(HttpClient client, HttpMethod metodo, string rota, object? corpo)
    {
        using HttpRequestMessage pedido = new(metodo, new Uri(rota, UriKind.Relative));
        pedido.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        pedido.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
        if (corpo is not null)
        {
            pedido.Content = JsonContent.Create(corpo);
            pedido.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        }

        return await client.SendAsync(pedido).ConfigureAwait(false);
    }
}
