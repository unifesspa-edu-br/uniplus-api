namespace Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Unidades;

using System.Net;
using System.Text.Json;
using System.Web;

/// <summary>Uma resposta da listagem de unidades: o status, os Ids da página e os links de navegação.</summary>
internal sealed record PaginaDeUnidades(
    HttpStatusCode Status, IReadOnlyList<Guid> Ids, string? Self, string? Next, string Corpo)
{
    private const string Listagem = "/api/organizacao/unidades";

    public static async Task<PaginaDeUnidades> LerAsync(HttpClient client, string url)
    {
        using HttpResponseMessage resposta = await client.GetAsync(new Uri(url, UriKind.RelativeOrAbsolute));
        string corpo = await resposta.Content.ReadAsStringAsync();
        return new PaginaDeUnidades(
            resposta.StatusCode, IdsDo(corpo, resposta.IsSuccessStatusCode), LinkDe(resposta, "self"),
            LinkDe(resposta, "next"), corpo);
    }

    /// <summary>Consulta a listagem com a query dada, sem o <c>?</c>.</summary>
    public static Task<PaginaDeUnidades> ConsultarAsync(HttpClient client, string query) =>
        LerAsync(client, $"{Listagem}?{query}");

    /// <summary>Percorre as páginas seguindo o link <c>next</c> até o fim.</summary>
    public static async Task<IReadOnlyList<PaginaDeUnidades>> PercorrerAsync(HttpClient client, string query)
    {
        List<PaginaDeUnidades> paginas = [await ConsultarAsync(client, query)];
        while (paginas[^1].Next is { } proxima)
        {
            paginas.Add(await LerAsync(client, proxima));
        }

        return paginas;
    }

    /// <summary>O valor decodificado do parâmetro na URL, ou <see langword="null"/> quando ela não o traz.</summary>
    public static string? ParametroDe(string url, string parametro) =>
        HttpUtility.ParseQueryString(new Uri(url).Query).Get(parametro);

    private static List<Guid> IdsDo(string corpo, bool sucesso)
    {
        if (!sucesso)
        {
            return [];
        }

        using JsonDocument documento = JsonDocument.Parse(corpo);
        return [.. documento.RootElement.EnumerateArray().Select(item => item.GetProperty("id").GetGuid())];
    }

    private static string? LinkDe(HttpResponseMessage resposta, string rel)
    {
        string? link = resposta.Headers.TryGetValues("Link", out IEnumerable<string>? valores) ? valores.Single() : null;
        string? parte = link?.Split(", ").FirstOrDefault(p => p.EndsWith($"rel=\"{rel}\"", StringComparison.Ordinal));
        return parte?[(parte.IndexOf('<', StringComparison.Ordinal) + 1)..parte.IndexOf('>', StringComparison.Ordinal)];
    }
}
