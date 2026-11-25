namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Cursos;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;


internal static class ConsultaDeCursos
{
    private const string Cursos = "/api/configuracao/cursos";

    internal static async Task<(List<string> Nomes, int Paginas)> PercorrerNomesAsync(HttpClient client, string url)
    {
        List<string> nomes = [];
        int paginas = 0;
        string? proxima = url;
        while (proxima is not null)
        {
            HttpResponseMessage resposta = await client.GetAsync(new Uri(proxima, UriKind.RelativeOrAbsolute));
            resposta.StatusCode.Should().Be(HttpStatusCode.OK);
            nomes.AddRange(await LerNomesAsync(resposta));
            paginas++;
            proxima = ApoioDaOrdenacaoAlfabetica.LinkRel(resposta, "next");
        }
        return (nomes, paginas);
    }

    internal static async Task<string> CursorDaPrimeiraPaginaAsync(HttpClient client, string url)
    {
        HttpResponseMessage primeira = await client.GetAsync(new Uri(url, UriKind.Relative));
        string proxima = ApoioDaOrdenacaoAlfabetica.LinkRel(primeira, "next").Should().NotBeNull().And.Subject.ToString()!;
        return ParametroCursor(proxima);
    }

    internal static async Task<HttpResponseMessage> VoltarPaginaAsync(HttpClient client, HttpResponseMessage primeira)
    {
        string proxima = ApoioDaOrdenacaoAlfabetica.LinkRel(primeira, "next").Should().NotBeNull().And.Subject.ToString()!;
        HttpResponseMessage segunda = await client.GetAsync(new Uri(proxima, UriKind.RelativeOrAbsolute));
        string anterior = ApoioDaOrdenacaoAlfabetica.LinkRel(segunda, "prev").Should().NotBeNull().And.Subject.ToString()!;
        return await client.GetAsync(new Uri(anterior, UriKind.RelativeOrAbsolute));
    }

    /// <summary>
    /// Extrai o par <c>cursor=...</c> de um link de navegação. O <c>?</c> inicial
    /// da query string sai junto: remontar a consulta com ele produziria uma URL
    /// malformada, em que o cursor é ignorado sem erro — e o teste passaria a
    /// afirmar o contrário do que pretende.
    /// </summary>
    internal static string ParametroCursor(string url) =>
        new Uri(url, UriKind.RelativeOrAbsolute).Query
            .TrimStart('?')
            .Split('&')
            .Single(p => p.StartsWith("cursor=", StringComparison.Ordinal));

    internal static string Bloco() => $"ZZ{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";

    internal static async Task<IReadOnlyList<string>> NomesDoBlocoAsync(
        HttpClient client, string bloco, string? consulta)
    {
        IReadOnlyList<JsonElement> itens = await ItensDoBlocoAsync(client, bloco, consulta);
        return [.. itens.Select(i => i.GetProperty("nome").GetString()!)];
    }

    internal static async Task<IReadOnlyList<JsonElement>> ItensDoBlocoAsync(
        HttpClient client, string bloco, string? consulta)
    {
        string url = consulta is null ? $"{Cursos}?limit=100" : $"{Cursos}?limit=100&{consulta}";
        IReadOnlyList<JsonElement> itens = await ListarItensAsync(client, url);
        return
        [
            .. itens.Where(i => i.GetProperty("nome").GetString()!
                .StartsWith(bloco, StringComparison.Ordinal)),
        ];
    }

    private static async Task<IReadOnlyList<JsonElement>> ListarItensAsync(HttpClient client, string endereco)
    {
        List<JsonElement> itens = [];
        string? url = endereco;

        while (url is not null)
        {
            HttpResponseMessage resposta = await client.GetAsync(new Uri(url, UriKind.RelativeOrAbsolute));
            resposta.StatusCode.Should().Be(HttpStatusCode.OK);

            JsonDocument documento = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
            itens.AddRange(documento.RootElement.EnumerateArray());
            url = ApoioDaOrdenacaoAlfabetica.LinkRel(resposta, "next");
        }

        return itens;
    }

    internal static async Task<IReadOnlyList<string>> LerNomesAsync(HttpResponseMessage resposta)
    {
        JsonDocument documento = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        return [.. documento.RootElement.EnumerateArray().Select(i => i.GetProperty("nome").GetString()!)];
    }

    internal static async Task<Guid> CriarCursoAsync(
        HttpClient client,
        string nome,
        string? codigo = null,
        string grau = "Bacharelado",
        string nivel = "Graduação")
    {
        HttpResponseMessage resposta = await ApoioDaOrdenacaoAlfabetica.PostAdminAsync(
            client, "/api/configuracao/admin/cursos", new
            {
                codigo = codigo ?? $"CUR_{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}",
                nome,
                grau,
                nivelEnsino = nivel,
            });
        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
        return await resposta.Content.ReadFromJsonAsync<Guid>();
    }
}
