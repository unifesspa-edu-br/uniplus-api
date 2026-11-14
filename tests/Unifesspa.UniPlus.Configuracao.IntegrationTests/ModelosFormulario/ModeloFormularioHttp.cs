namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.ModelosFormulario;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

/// <summary>
/// Chamadas HTTP e leitura das respostas que os testes dos modelos de formulário compartilham, feitas
/// com a pessoa que mantém o cadastro.
/// </summary>
internal static class ModeloFormularioHttp
{
    internal const string Base = "/api/configuracao/admin/modelos-formulario";

    /// <summary>Marabá, município da área do bônus regional.</summary>
    internal const string Maraba = "1504208";

    internal static async Task<HttpResponseMessage> EnviarAsync(
        HttpClient client, HttpMethod metodo, string url, object? corpo = null)
    {
        using HttpRequestMessage request = NovaRequisicao(metodo, url, corpo);
        return await client.SendAsync(request);
    }

    internal static async Task<HttpStatusCode> StatusAsync(
        HttpClient client, HttpMethod metodo, string url, object? corpo = null) =>
        (await EnviarAsync(client, metodo, url, corpo)).StatusCode;

    internal static async Task<JsonObject> LerAsync(HttpResponseMessage resposta)
    {
        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, corpo);
        return JsonNode.Parse(corpo)!.AsObject();
    }

    internal static async Task<Guid> CriarAsync(HttpClient client, object modelo)
    {
        HttpResponseMessage criar = await EnviarAsync(client, HttpMethod.Post, Base, modelo);
        criar.StatusCode.Should().Be(HttpStatusCode.Created, await criar.Content.ReadAsStringAsync());
        return await criar.Content.ReadFromJsonAsync<Guid>();
    }

    internal static async Task CadastrarAsync(HttpClient client, string url, object corpo)
    {
        HttpResponseMessage criada = await EnviarAsync(client, HttpMethod.Post, url, corpo);
        criada.StatusCode.Should().Be(HttpStatusCode.Created, await criada.Content.ReadAsStringAsync());
    }

    /// <summary>Uma base legal do bônus com os municípios dados, de onde vêm as opções de município do Fator.</summary>
    internal static async Task CadastrarBaseLegalAsync(HttpClient client, object[] municipios) =>
        await CadastrarAsync(client, "/api/configuracao/admin/base-legal-bonus-regional", new
        {
            tipoInstrumento = "PORTARIA",
            identificacao = $"Portaria {Guid.NewGuid().ToString("N")[..8]}",
            descricao = "Área do bônus regional",
            municipios,
        });

    internal static object Municipio(string codigoIbge, string nome) => new { codigoIbge, nome, uf = "PA" };

    internal static async Task<JsonObject> ObterAsync(HttpClient client, Guid id) =>
        await LerAsync(await EnviarAsync(client, HttpMethod.Get, $"{Base}/{id}"));

    internal static async Task<JsonObject> RenderizavelAsync(HttpClient client, Guid id) =>
        await LerAsync(await EnviarAsync(client, HttpMethod.Get, $"{Base}/{id}/renderizavel"));

    internal static async Task<JsonDocument> ListarAsync(HttpClient client, string filtros)
    {
        HttpResponseMessage resposta = await EnviarAsync(client, HttpMethod.Get, $"{Base}{filtros}");
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        return JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
    }

    internal static async Task<Guid> IdDoModeloAsync(HttpClient client, string finalidade, string codigo)
    {
        using JsonDocument lista = await ListarAsync(client, $"?finalidade={finalidade}");
        return lista.RootElement.EnumerateArray()
            .Single(m => m.GetProperty("codigo").GetString() == codigo).GetProperty("id").GetGuid();
    }

    internal static async Task<JsonObject> PreVisualizacaoAsync(
        HttpClient client, Guid id, Dictionary<string, object> respostas) =>
        await LerAsync(await EnviarAsync(client, HttpMethod.Post, $"{Base}/{id}/pre-visualizacao", new { respostas }));

    internal static async Task<JsonArray> PreVisualizarAsync(
        HttpClient client, Guid id, Dictionary<string, object> respostas) =>
        (await PreVisualizacaoAsync(client, id, respostas))["itens"]!.AsArray();

    internal static JsonNode Unico(JsonNode lista, string chave, string valor) =>
        lista.AsArray().Single(e => e![chave]!.GetValue<string>() == valor)!;

    internal static JsonNode Item(JsonArray itens, string fatoCodigo) => Unico(itens, "fatoCodigo", fatoCodigo);

    internal static IEnumerable<string> Textos(JsonNode lista, string chave) =>
        lista.AsArray().Select(e => e![chave]!.GetValue<string>());

    internal static JsonNode OpcaoDoFato(JsonObject formulario, string fatoCodigo, string codigo) =>
        Unico(Unico(formulario["fatosColetados"]!, "fatoCodigo", fatoCodigo)["valoresSelecionaveis"]!, "codigo", codigo);

    internal static List<string> Opcoes(JsonObject formulario, string fatoCodigo) =>
        [.. Textos(Unico(formulario["fatosColetados"]!, "fatoCodigo", fatoCodigo)["valoresSelecionaveis"]!, "codigo")];

    internal static IEnumerable<string> DasEtapasDasRegras(JsonObject formulario, string colecao, string chave) =>
        formulario["regras"]!["etapas"]!.AsArray()
            .SelectMany(e => e![colecao]!.AsArray())
            .Select(i => i![chave]!.GetValue<string>());

    internal static List<string> ItensDaEtapaDasRegras(JsonObject formulario, string etapaCodigo) =>
        [.. Textos(Unico(formulario["regras"]!["etapas"]!, "codigo", etapaCodigo)["itens"]!, "fatoCodigo")];

    internal static string CodigoDe(string prefixo, int tamanho) =>
        $"{prefixo}_{Guid.NewGuid().ToString("N")[..tamanho].ToUpperInvariant()}";

    internal static string CodigoUnico() => CodigoDe("MODELO", 12);

    private static HttpRequestMessage NovaRequisicao(HttpMethod metodo, string url, object? corpo)
    {
        HttpRequestMessage request = new(metodo, new Uri(url, UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
        if (metodo != HttpMethod.Get)
        {
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        }

        request.Content = corpo is null ? null : JsonContent.Create(corpo);
        return request;
    }
}
