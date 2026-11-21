namespace Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

/// <summary>
/// O processo seletivo pelo HTTP, para os testes que tratam do formulário dele e não da autenticação: a requisição
/// administrativa, a leitura do processo e a gravação dos itens da inscrição.
/// </summary>
internal static class ProcessoSeletivoHttp
{
    private const string TipoDeMidiaDoProcesso = "application/vnd.uniplus.processo-seletivo.v1+json";

    /// <summary>A requisição com o token de teste e o papel da plataforma.</summary>
    public static HttpRequestMessage Requisicao(HttpMethod metodo, string rota)
    {
        HttpRequestMessage request = new(metodo, new Uri(rota, UriKind.Relative));
        request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
        return request;
    }

    /// <summary>O processo como o GET o devolve; a resposta que não é 200 falha o teste.</summary>
    public static async Task<JsonDocument> ObterAsync(HttpClient client, Guid processoId)
    {
        using HttpRequestMessage get = Requisicao(HttpMethod.Get, $"/api/selecao/processos-seletivos/{processoId}");
        get.Headers.Accept.ParseAdd(TipoDeMidiaDoProcesso);
        HttpResponseMessage lido = await client.SendAsync(get);
        lido.StatusCode.Should().Be(HttpStatusCode.OK, await lido.Content.ReadAsStringAsync());
        return JsonDocument.Parse(await lido.Content.ReadAsStringAsync());
    }

    /// <summary>O PUT dos itens da inscrição com o corpo dado.</summary>
    public static async Task<HttpResponseMessage> DefinirItensAsync(HttpClient client, Guid processoId, object corpo)
    {
        using HttpRequestMessage put = Requisicao(HttpMethod.Put, $"/api/selecao/admin/processos-seletivos/{processoId}/formularios/INSCRICAO/itens");
        put.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        put.Content = JsonContent.Create(corpo);
        return await client.SendAsync(put);
    }
}
