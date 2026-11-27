namespace Unifesspa.UniPlus.IntegrationTests.Fixtures.Paginacao;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

/// <summary>
/// Uma listagem paginada por cursor consultada por HTTP: primeira página, continuação por link e percurso completo.
/// Dona do cliente HTTP que recebe.
/// </summary>
public sealed class ConsultaPaginada : IDisposable
{
    private readonly HttpClient _cliente;
    private readonly string _rota;
    private readonly string[] _caminhoDoId;

    /// <param name="cliente">Cliente já identificado como a pessoa da consulta.</param>
    /// <param name="rota">Rota da listagem.</param>
    /// <param name="propriedadeDoId">
    /// Propriedade de cada item que traz o identificador, quando não é <c>id</c>; para o identificador dentro de um objeto do
    /// item, os nomes separados por ponto, como <c>fato.id</c>.
    /// </param>
    public ConsultaPaginada(HttpClient cliente, string rota, string propriedadeDoId = "id")
    {
        ArgumentNullException.ThrowIfNull(cliente);
        ArgumentException.ThrowIfNullOrWhiteSpace(rota);
        ArgumentException.ThrowIfNullOrWhiteSpace(propriedadeDoId);

        _cliente = cliente;
        _rota = rota;
        _caminhoDoId = propriedadeDoId.Split('.');
    }

    public void Dispose() => _cliente.Dispose();

    public Task<HttpResponseMessage> ObterAsync(string endereco) =>
        _cliente.GetAsync(new Uri(endereco, UriKind.RelativeOrAbsolute));

    public Task<HttpResponseMessage> PrimeiraAsync(string consulta, int limite = 1) =>
        ObterAsync($"{_rota}?{ComLimite(consulta, limite)}");

    /// <summary>O link <c>next</c> da primeira página, que precisa existir: a listagem tem mais de uma página.</summary>
    public async Task<string> ProximoAsync(string consulta)
    {
        using HttpResponseMessage primeira = await PrimeiraAsync(consulta).ConfigureAwait(false);
        await RespostaDaListagem.ConferirStatusAsync(primeira, HttpStatusCode.OK).ConfigureAwait(false);

        return LinkDePaginacao.Proximo(primeira).Should().NotBeNull("a consulta tem mais de uma página").And.Subject!;
    }

    /// <summary>O link <c>prev</c> da segunda página, que precisa existir.</summary>
    public async Task<string> AnteriorDaSegundaAsync(string consulta)
    {
        using HttpResponseMessage segunda = await ObterAsync(await ProximoAsync(consulta).ConfigureAwait(false)).ConfigureAwait(false);
        await RespostaDaListagem.ConferirStatusAsync(segunda, HttpStatusCode.OK).ConfigureAwait(false);

        return LinkDePaginacao.Anterior(segunda).Should().NotBeNull("a segunda página tem a anterior").And.Subject!;
    }

    /// <summary>
    /// Percorre todas as páginas pelo link <c>next</c>, reescrito por <paramref name="ajuste"/> a cada continuação, e
    /// devolve os identificadores na ordem em que vieram.
    /// </summary>
    public async Task<IReadOnlyList<Guid>> PercorrerAsync(string consulta, int limite, Func<string, string>? ajuste = null)
    {
        List<Guid> ids = [];
        string? endereco = $"{_rota}?{ComLimite(consulta, limite)}";
        while (endereco is not null)
        {
            using HttpResponseMessage pagina = await ObterAsync(endereco).ConfigureAwait(false);
            string corpo = await RespostaDaListagem.ConferirStatusAsync(pagina, HttpStatusCode.OK).ConfigureAwait(false);
            ids.AddRange(IdsDe(corpo));
            endereco = LinkDePaginacao.Proximo(pagina) is { } proximo ? (ajuste?.Invoke(proximo) ?? proximo) : null;
        }

        return ids;
    }

    private static string ComLimite(string consulta, int limite) =>
        string.IsNullOrEmpty(consulta) ? $"limit={limite}" : $"{consulta}&limit={limite}";

    private IEnumerable<Guid> IdsDe(string corpo)
    {
        using JsonDocument documento = JsonDocument.Parse(corpo);
        JsonElement itens = documento.RootElement.ValueKind == JsonValueKind.Array
            ? documento.RootElement
            : documento.RootElement.GetProperty("items");

        return [.. itens.EnumerateArray().Select(IdDe)];
    }

    private Guid IdDe(JsonElement item) =>
        _caminhoDoId.Aggregate(item, static (atual, nome) => atual.GetProperty(nome)).GetGuid();
}
