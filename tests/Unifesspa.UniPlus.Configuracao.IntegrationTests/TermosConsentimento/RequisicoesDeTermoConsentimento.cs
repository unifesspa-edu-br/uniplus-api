namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.TermosConsentimento;

using System.Net;
using System.Net.Http.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

internal static class RequisicoesDeTermoConsentimento
{
    private const string AdminPath = "/api/configuracao/admin/termos-consentimento";

    internal static object CorpoValido() => new
    {
        nome = $"Termo LGPD {Guid.NewGuid():N}",
        textoRascunho = "Texto do termo",
        baseLegalRascunho = "Lei 13.709/2018",
        formaAceiteRascunho = "REGISTRO_SEM_ENDERECO_IP",
    };

    internal static object CorpoVazio() => new
    {
        nome = $"Termo em branco {Guid.NewGuid():N}",
        textoRascunho = (string?)null,
        baseLegalRascunho = (string?)null,
        formaAceiteRascunho = (string?)null,
    };

    internal static async Task<Guid> CriarTermo(HttpClient client, object corpo)
    {
        HttpResponseMessage criar = await EnviarAdmin(client, HttpMethod.Post, AdminPath, corpo);
        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        return await criar.Content.ReadFromJsonAsync<Guid>();
    }

    internal static Task<Guid> CriarTermoComNome(HttpClient client, string nome) => CriarTermo(client, new
    {
        nome,
        textoRascunho = (string?)null,
        baseLegalRascunho = (string?)null,
        formaAceiteRascunho = (string?)null,
    });

    internal static async Task<HttpResponseMessage> EnviarAdmin(HttpClient client, HttpMethod method, string path, object? body)
    {
        using HttpRequestMessage request = new(method, new Uri(path, UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }

    internal static async Task<HttpResponseMessage> EnviarDeleteAdmin(HttpClient client, Guid id)
    {
        using HttpRequestMessage request = new(
            HttpMethod.Delete, new Uri($"{AdminPath}/{id}", UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
        return await client.SendAsync(request);
    }
}
