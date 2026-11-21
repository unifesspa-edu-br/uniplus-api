namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.TiposEtapa;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

/// <summary>Montagens comuns dos testes do endpoint de tipos de etapa: corpo de escrita, chamadas autenticadas e leitura de um tipo.</summary>
internal static class ApoioDoEndpointDeTiposEtapa
{
    internal static async Task<JsonDocument> ObterTipo(HttpClient client, Guid id)
    {
        HttpResponseMessage obter = await client.GetAsync(new Uri($"/api/configuracao/tipos-etapa/{id}", UriKind.Relative));
        obter.StatusCode.Should().Be(HttpStatusCode.OK);
        return JsonDocument.Parse(await obter.Content.ReadAsStringAsync());
    }

    // Corpo válido de escrita; cada cenário troca só o que o interessa.
    // Os campos da avaliação vão para dentro de "avaliacao"; os demais ficam na raiz do corpo.
    internal static Dictionary<string, object?> Corpo(params (string Campo, object? Valor)[] trocas)
    {
        Dictionary<string, object?> corpo = CorpoPadrao();
        Dictionary<string, object?> avaliacao = (Dictionary<string, object?>)corpo["avaliacao"]!;
        foreach ((string campo, object? valor) in trocas)
        {
            (CamposDaAvaliacao.Contains(campo) ? avaliacao : corpo)[campo] = valor;
        }

        return corpo;
    }

    internal static readonly string[] CamposDaAvaliacao = ["conceitosAdmitidos", "admitePontuacao", "admiteEliminacao", "exigeCorrecaoIndependente", "notaDeOrigemNoEnem"];

    internal static Dictionary<string, object?> CorpoPadrao() => new(StringComparer.Ordinal)
    {
        ["codigo"] = CodigoUnico(),
        ["nome"] = "Nome válido",
        ["descricao"] = "Descrição válida",
        ["avaliacao"] = new Dictionary<string, object?>
        {
            ["conceitosAdmitidos"] = Array.Empty<string>(),
            ["admitePontuacao"] = true,
            ["admiteEliminacao"] = true,
            ["exigeCorrecaoIndependente"] = false,
        },
    };

    internal static string CodigoUnico() => $"PS_{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}";


    internal static async Task<HttpResponseMessage> EnviarPostAdmin(HttpClient client, object body)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri("/api/configuracao/admin/tipos-etapa", UriKind.Relative));
        AutenticarComoAdmin(request);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }

    internal static async Task<HttpResponseMessage> EnviarPutAdmin(HttpClient client, Guid id, object body)
    {
        using HttpRequestMessage request = new(HttpMethod.Put, new Uri($"/api/configuracao/admin/tipos-etapa/{id}", UriKind.Relative));
        AutenticarComoAdmin(request);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }

    internal static async Task<HttpResponseMessage> EnviarDeleteAdmin(HttpClient client, Guid id)
    {
        using HttpRequestMessage request = new(HttpMethod.Delete, new Uri($"/api/configuracao/admin/tipos-etapa/{id}", UriKind.Relative));
        AutenticarComoAdmin(request);
        return await client.SendAsync(request);
    }

    internal static void AutenticarComoAdmin(HttpRequestMessage request)
    {
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
    }
}
