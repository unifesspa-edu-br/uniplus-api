namespace Unifesspa.UniPlus.IntegrationTests.Fixtures.Paginacao;

using System.Collections.Specialized;
using System.Web;

/// <summary>Lê e reescreve os links <c>next</c> e <c>prev</c> que a listagem devolve no cabeçalho <c>Link</c>.</summary>
public static class LinkDePaginacao
{
    private const string ParametroDoCursor = "cursor";

    public static string? Proximo(HttpResponseMessage resposta) => Rel(resposta, "next");

    public static string? Anterior(HttpResponseMessage resposta) => Rel(resposta, "prev");

    public static string? Rel(HttpResponseMessage resposta, string rel)
    {
        ArgumentNullException.ThrowIfNull(resposta);

        return resposta.Headers.TryGetValues("Link", out IEnumerable<string>? valores)
            ? valores.SelectMany(static v => v.Split(", ", StringSplitOptions.RemoveEmptyEntries))
                .Where(parte => parte.EndsWith($"rel=\"{rel}\"", StringComparison.Ordinal))
                .Select(EnderecoEntreSinais)
                .FirstOrDefault()
            : null;
    }

    public static string Cursor(string link) => Valor(link, ParametroDoCursor)
        ?? throw new InvalidOperationException("O link não traz o parâmetro cursor.");

    public static string ComCursor(string link, string cursor) => ComParametro(link, ParametroDoCursor, cursor);

    /// <summary>Troca todos os valores do parâmetro pelos informados; sem valores, retira o parâmetro.</summary>
    public static string ComParametro(string link, string nome, params string[] valores)
    {
        ArgumentNullException.ThrowIfNull(link);
        ArgumentNullException.ThrowIfNull(valores);

        NameValueCollection parametros = ParametrosDe(link);
        parametros.Remove(nome);
        foreach (string valor in valores)
        {
            parametros.Add(nome, valor);
        }

        return $"{LinkSemConsulta(link)}?{parametros}";
    }

    public static string SemParametro(string link, string nome) => ComParametro(link, nome);

    public static string? Valor(string link, string nome)
    {
        ArgumentNullException.ThrowIfNull(link);

        return ParametrosDe(link)[nome];
    }

    private static NameValueCollection ParametrosDe(string link)
    {
        int inicio = link.IndexOf('?', StringComparison.Ordinal);

        return HttpUtility.ParseQueryString(inicio < 0 ? string.Empty : link[(inicio + 1)..]);
    }

    private static string LinkSemConsulta(string link)
    {
        int inicio = link.IndexOf('?', StringComparison.Ordinal);

        return inicio < 0 ? link : link[..inicio];
    }

    private static string? EnderecoEntreSinais(string parte)
    {
        int inicio = parte.IndexOf('<', StringComparison.Ordinal) + 1;
        int fim = parte.IndexOf('>', StringComparison.Ordinal);

        return inicio > 0 && fim > inicio ? parte[inicio..fim] : null;
    }
}
