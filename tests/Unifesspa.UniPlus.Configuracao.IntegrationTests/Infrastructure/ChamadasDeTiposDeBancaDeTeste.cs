namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

/// <summary>As chamadas administrativas de cadastro de tipos de banca que os testes de endpoint repetem.</summary>
internal static class ChamadasDeTiposDeBancaDeTeste
{
    public static async Task<HttpResponseMessage> PostAsync(HttpClient client, object corpo)
    {
        using HttpRequestMessage requisicao = Administrativa(HttpMethod.Post, "/api/configuracao/admin/tipos-banca");
        requisicao.Content = JsonContent.Create(corpo);
        return await client.SendAsync(requisicao);
    }

    public static async Task<HttpResponseMessage> PutAsync(HttpClient client, Guid id, object corpo)
    {
        using HttpRequestMessage requisicao = Administrativa(HttpMethod.Put, $"/api/configuracao/admin/tipos-banca/{id}");
        requisicao.Content = JsonContent.Create(corpo);
        return await client.SendAsync(requisicao);
    }

    /// <summary>Cadastra o tipo, confere o 201 e devolve a leitura dele.</summary>
    public static async Task<JsonDocument> CriarELerAsync(HttpClient client, object corpo)
    {
        HttpResponseMessage criado = await PostAsync(client, corpo);
        criado.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid id = await criado.Content.ReadFromJsonAsync<Guid>();

        return await LerAsync(client, id);
    }

    public static async Task<JsonDocument> LerAsync(HttpClient client, Guid id)
    {
        HttpResponseMessage resposta = await client.GetAsync(new Uri($"/api/configuracao/tipos-banca/{id}", UriKind.Relative));
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);

        return JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
    }

    /// <summary>O campo e o código publicado de cada erro do <c>errors[]</c> da recusa, na ordem em que vieram.</summary>
    public static (string? Campo, string? Codigo)[] ErrosDe(string corpoDaRecusa)
    {
        using JsonDocument documento = JsonDocument.Parse(corpoDaRecusa);

        return [.. documento.RootElement.GetProperty("errors").EnumerateArray()
            .Select(static erro => (erro.GetProperty("field").GetString(), erro.GetProperty("code").GetString()))];
    }

    private static HttpRequestMessage Administrativa(HttpMethod metodo, string rota)
    {
        HttpRequestMessage requisicao = new(metodo, new Uri(rota, UriKind.Relative));
        requisicao.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        requisicao.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
        requisicao.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        return requisicao;
    }
}
