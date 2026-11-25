namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.TiposProcesso;

using System.Net;
using System.Net.Http.Json;
using System.Text;

using AwesomeAssertions;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

internal static class RequisicoesDeTipoProcesso
{
    internal static async Task<HttpResponseMessage> EnviarGetAdmin(HttpClient client, string rota)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, new Uri(rota, UriKind.Relative));
        AutenticarComoAdmin(request);
        return await client.SendAsync(request);
    }

    internal static async Task<HttpResponseMessage> EnviarAtivacaoAdmin(HttpClient client, Guid id)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri($"/api/configuracao/admin/tipos-processo/{id}/ativacao", UriKind.Relative));
        AutenticarComoAdmin(request);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        return await client.SendAsync(request);
    }

    internal static async Task<HttpResponseMessage> EnviarPostAdmin(HttpClient client, object body)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri("/api/configuracao/admin/tipos-processo", UriKind.Relative));
        AutenticarComoAdmin(request);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }

    internal static async Task<HttpResponseMessage> EnviarPutAdmin(HttpClient client, Guid id, object body)
    {
        using HttpRequestMessage request = new(HttpMethod.Put, new Uri($"/api/configuracao/admin/tipos-processo/{id}", UriKind.Relative));
        AutenticarComoAdmin(request);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }

    internal static async Task<HttpResponseMessage> EnviarDeleteAdmin(HttpClient client, Guid id)
    {
        using HttpRequestMessage request = new(HttpMethod.Delete, new Uri($"/api/configuracao/admin/tipos-processo/{id}", UriKind.Relative));
        AutenticarComoAdmin(request);
        return await client.SendAsync(request);
    }

    internal static void AutenticarComoAdmin(HttpRequestMessage request)
    {
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
    }

    internal static async Task<Guid> CriarTipoInicial(HttpClient client, string codigo)
    {
        HttpResponseMessage criar = await EnviarPostAdmin(client, new
        {
            codigo,
            nome = "Seleção de teste",
            descricao = "Descrição inicial",
        });

        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();
        id.Should().NotBe(Guid.Empty);
        return id;
    }
    internal static async Task<Guid> CriarTipoComCamposValidos(HttpClient client)
    {
        HttpResponseMessage criar = await EnviarPostAdmin(client, new
        {
            codigo = CodigoUnico(),
            nome = "Nome válido",
            descricao = "Descrição válida",
        });
        return await criar.Content.ReadFromJsonAsync<Guid>();
    }
    internal static string CodigoUnico() => $"PS_{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}";

    internal static object CamposDeCriacaoComCaractereNulo(string campo)
    {
        string invalido = $"valor{(char)0}invalido";
        return campo switch
        {
            "codigo" => new { codigo = invalido, nome = "Nome válido", descricao = "Descrição válida" },
            "nome" => new { codigo = CodigoUnico(), nome = invalido, descricao = "Descrição válida" },
            "descricao" => new { codigo = CodigoUnico(), nome = "Nome válido", descricao = invalido },
            _ => throw new InvalidOperationException($"Campo de teste inesperado: {campo}"),
        };
    }

    internal static object CamposDeAtualizacaoComCaractereNulo(Guid id, string campo)
    {
        string invalido = $"valor{(char)0}invalido";
        return campo switch
        {
            "nome" => new { id, nome = invalido, descricao = "Descrição válida" },
            "descricao" => new { id, nome = "Nome válido", descricao = invalido },
            _ => throw new InvalidOperationException($"Campo de teste inesperado: {campo}"),
        };
    }

    internal static async Task<HttpResponseMessage> EnviarJsonAdmin(HttpClient client, string json)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri("/api/configuracao/admin/tipos-processo", UriKind.Relative));
        AutenticarComoAdmin(request);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        return await client.SendAsync(request);
    }

}
