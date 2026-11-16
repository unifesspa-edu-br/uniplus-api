namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.FasesCanonicas;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Npgsql;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

internal static class ApoioDosEndpointsDeFaseCanonica
{
    internal static async Task<Guid> CriarAsync(HttpClient client, object body)
    {
        HttpResponseMessage criar = await EnviarPostAdmin(client, body);
        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();
        id.Should().NotBe(Guid.Empty);
        return id;
    }

    internal static async Task<JsonElement> ObterAsync(HttpClient client, Guid id)
    {
        HttpResponseMessage obter = await client.GetAsync(
            new Uri($"/api/configuracao/fases-canonicas/{id}", UriKind.Relative));
        obter.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument doc = JsonDocument.Parse(await obter.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }

    internal static async Task<HttpResponseMessage> EnviarPutAdmin(HttpClient client, Guid id, object body)
    {
        using HttpRequestMessage request = new(HttpMethod.Put, new Uri($"/api/configuracao/admin/fases-canonicas/{id}", UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }

    internal static async Task<HttpResponseMessage> EnviarPostAdmin(HttpClient client, object body)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri("/api/configuracao/admin/fases-canonicas", UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }
    internal static async Task LiberarSlotDoSeedAsync(string stringDeConexao, string codigo)
    {
        await using var conexao = new NpgsqlConnection(stringDeConexao);
        await conexao.OpenAsync();

        await using NpgsqlCommand comando = conexao.CreateCommand();
        comando.CommandText =
            "DELETE FROM configuracao.fases_canonicas " +
            "WHERE id::text LIKE 'f45e0000-0000-7000-8000-%' AND codigo = @codigo";
        comando.Parameters.AddWithValue("codigo", codigo);
        await comando.ExecuteNonQueryAsync();
    }

}
