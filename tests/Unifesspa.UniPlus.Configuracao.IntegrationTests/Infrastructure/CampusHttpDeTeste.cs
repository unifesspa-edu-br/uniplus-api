namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

using System.Net.Http.Json;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

/// <summary>Requisições ao cadastro de campi pela porta pública, como o administrador dos testes.</summary>
internal static class CampusHttpDeTeste
{
    public static string NovaSigla() => $"C{Guid.NewGuid().ToString("N")[..6]}";

    public static HttpRequestMessage Criacao(string sigla, string chaveDeIdempotencia)
    {
        HttpRequestMessage requisicao = new(HttpMethod.Post, new Uri("/api/configuracao/admin/campi", UriKind.Relative));
        requisicao.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        requisicao.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
        requisicao.Headers.Add("Idempotency-Key", chaveDeIdempotencia);
        requisicao.Content = JsonContent.Create(new
        {
            sigla,
            nome = "Campus de Teste",
            cidadeCodigoIbge = "1504208",
            cidadeNome = "Marabá",
            cidadeUf = "PA",
        });
        return requisicao;
    }

    public static async Task<HttpResponseMessage> CriarAsync(HttpClient client, string sigla, string chaveDeIdempotencia)
    {
        using HttpRequestMessage requisicao = Criacao(sigla, chaveDeIdempotencia);
        return await client.SendAsync(requisicao).ConfigureAwait(false);
    }

    public static string? LinkRel(HttpResponseMessage resposta, string rel)
    {
        if (!resposta.Headers.TryGetValues("Link", out IEnumerable<string>? links))
        {
            return null;
        }

        string? parte = links.Single()
            .Split(", ", StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(p => p.Contains($"rel=\"{rel}\"", StringComparison.Ordinal));
        return parte is null ? null : parte[(parte.IndexOf('<', StringComparison.Ordinal) + 1)..parte.IndexOf('>', StringComparison.Ordinal)];
    }
}
