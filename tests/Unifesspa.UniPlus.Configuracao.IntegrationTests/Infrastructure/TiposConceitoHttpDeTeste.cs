namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

/// <summary>
/// Requisições ao catálogo de tipos de conceito pela porta pública. Os corpos são dicionários para o teste
/// trocar ou tirar um campo sem montar outro tipo anônimo; sem <c>sub</c>, o autor é o administrador dos testes.
/// </summary>
internal static class TiposConceitoHttpDeTeste
{
    public const string Publico = "/api/configuracao/tipos-conceito";
    public const string Admin = "/api/configuracao/admin/tipos-conceito";

    public static string CodigoUnico() => $"CT_{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}";

    public static Dictionary<string, object?> Numerico(string? codigo = null) => new()
    {
        ["codigo"] = codigo ?? CodigoUnico(),
        ["nome"] = "Nota da entrevista",
        ["descricao"] = "Nota atribuída pela banca.",
        ["natureza"] = "numerico",
        ["numerico"] = new { escalaMinima = 0m, escalaMaxima = 10m, casasDecimais = 2 },
    };

    public static Dictionary<string, object?> Categorico(string? codigo = null) => new()
    {
        ["codigo"] = codigo ?? CodigoUnico(),
        ["nome"] = "Parecer da análise",
        ["natureza"] = "categorico",
        ["valores"] = new object[]
        {
            new { codigo = "INDEFERIDO", rotulo = "Indeferido", contaComoAprovacao = false },
            new { codigo = "DEFERIDO", rotulo = "Deferido", contaComoAprovacao = true },
        },
    };

    public static Dictionary<string, object?> Com(this Dictionary<string, object?> corpo, string campo, object? valor)
    {
        ArgumentNullException.ThrowIfNull(corpo);
        corpo[campo] = valor;
        return corpo;
    }

    public static Dictionary<string, object?> Sem(this Dictionary<string, object?> corpo, string campo)
    {
        ArgumentNullException.ThrowIfNull(corpo);
        corpo.Remove(campo);
        return corpo;
    }

    public static HttpRequestMessage Autenticada(HttpMethod metodo, string rota, string? sub = null)
    {
        HttpRequestMessage requisicao = new(metodo, new Uri(rota, UriKind.Relative));
        requisicao.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        requisicao.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
        if (sub is not null)
        {
            requisicao.Headers.Add(TestAuthHandler.UserIdHeader, sub);
        }

        return requisicao;
    }

    public static async Task<HttpResponseMessage> PostarAsync(HttpClient client, object corpo, string? sub = null, string? chave = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        using HttpRequestMessage requisicao = Autenticada(HttpMethod.Post, Admin, sub);
        requisicao.Headers.Add("Idempotency-Key", chave ?? Guid.NewGuid().ToString());
        requisicao.Content = JsonContent.Create(corpo);
        return await client.SendAsync(requisicao);
    }

    public static async Task<HttpResponseMessage> AtualizarAsync(HttpClient client, Guid id, object corpo, string? sub = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        using HttpRequestMessage requisicao = Autenticada(HttpMethod.Put, $"{Admin}/{id}", sub);
        requisicao.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        requisicao.Content = JsonContent.Create(corpo);
        return await client.SendAsync(requisicao);
    }

    public static async Task<HttpResponseMessage> DesativarAsync(HttpClient client, Guid id, string? sub = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        using HttpRequestMessage requisicao = Autenticada(HttpMethod.Delete, $"{Admin}/{id}", sub);
        return await client.SendAsync(requisicao);
    }

    public static Task<HttpResponseMessage> ObterAsync(HttpClient client, Guid id) =>
        client.GetAsync(new Uri($"{Publico}/{id}", UriKind.Relative));

    public static async Task<Guid> CriarAsync(HttpClient client, Dictionary<string, object?> corpo)
    {
        using HttpResponseMessage resposta = await PostarAsync(client, corpo);
        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
        return await resposta.Content.ReadFromJsonAsync<Guid>();
    }

    public static async Task<JsonDocument> LerAsync(HttpResponseMessage resposta)
    {
        ArgumentNullException.ThrowIfNull(resposta);
        return JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
    }

    public static async Task<IReadOnlyList<Guid>> IdsAtivosAsync(HttpClient client, int limite)
    {
        List<Guid> ids = [];
        string? url = $"{Publico}?limit={limite}";
        while (url is not null)
        {
            using HttpResponseMessage pagina = await client.GetAsync(new Uri(url, UriKind.RelativeOrAbsolute));
            pagina.StatusCode.Should().Be(HttpStatusCode.OK);
            ids.AddRange(await IdsDaPaginaAsync(pagina));
            url = CampusHttpDeTeste.LinkRel(pagina, "next");
        }

        return ids;
    }

    public static async Task<IReadOnlyList<Guid>> IdsDaPaginaAsync(HttpResponseMessage pagina)
    {
        using JsonDocument lista = await LerAsync(pagina);
        return [.. lista.RootElement.EnumerateArray().Select(item => item.GetProperty("id").GetGuid())];
    }

    public static bool Ausente(JsonElement objeto, string campo) =>
        !objeto.TryGetProperty(campo, out JsonElement valor) || valor.ValueKind == JsonValueKind.Null;
}
