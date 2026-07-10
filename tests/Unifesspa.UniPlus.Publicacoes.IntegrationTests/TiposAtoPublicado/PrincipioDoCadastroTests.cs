namespace Unifesspa.UniPlus.Publicacoes.IntegrationTests.TiposAtoPublicado;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Publicacoes.IntegrationTests.Infrastructure;

/// <summary>
/// O princípio da ADR-0103, exercitado pela API: acrescentar um tipo de ato é linha
/// de cadastro, e dois tipos com a mesma configuração comportam-se identicamente.
/// </summary>
/// <remarks>
/// <para>A prova estrutural — nenhum <c>if</c> por código de tipo em
/// <c>src/publicacoes/</c> — vive no fitness test
/// <c>PublicacoesSemRamificacaoPorTipoAtoTests</c>. Aqui prova-se o comportamento:
/// um código nunca visto pelo código-fonte percorre todo o ciclo de vida sem que
/// nada precise saber o que ele significa.</para>
/// <para>Se um ramo por código existisse, os dois tipos desta suíte divergiriam —
/// e nenhum teste de status os pegaria, porque ambos responderiam 200.</para>
/// </remarks>
[Collection(PublicacoesEndpointCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class PrincipioDoCadastroTests
{
    private const string Base = "/api/publicacoes/tipos-ato";
    private const string Admin = "/api/publicacoes/admin/tipos-ato";

    private readonly PublicacoesEndpointFixture _fixture;

    public PrincipioDoCadastroTests(PublicacoesEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Dois tipos com a mesma configuração comportam-se identicamente")]
    public async Task DoisTiposComMesmaConfiguracao_SaoIndistinguiveis()
    {
        string alfa = CodigoUnico();
        string beta = CodigoUnico();

        using HttpClient client = _fixture.Factory.CreateClient();

        Guid idAlfa = await CriarAsync(client, alfa, congela: true, unico: true, irreversivel: true);
        Guid idBeta = await CriarAsync(client, beta, congela: true, unico: true, irreversivel: true);

        JsonElement corpoAlfa = await LerAsync(client, $"{Base}/{idAlfa}");
        JsonElement corpoBeta = await LerAsync(client, $"{Base}/{idBeta}");

        // Mesmas chaves, mesma forma. O que varia é a identidade e o rótulo.
        Nomes(corpoAlfa).Should().BeEquivalentTo(Nomes(corpoBeta));

        foreach (string campo in new[] { "congelaConfiguracao", "unicoPorObjeto", "efeitoIrreversivel" })
        {
            corpoAlfa.GetProperty(campo).GetBoolean()
                .Should().Be(corpoBeta.GetProperty(campo).GetBoolean(), $"{campo} foi informado igual nos dois");
        }

        // E o mesmo vale para a resolução por data e para o ciclo de escrita.
        (await LerAsync(client, $"{Base}/{alfa}/vigente")).GetProperty("id").GetGuid().Should().Be(idAlfa);
        (await LerAsync(client, $"{Base}/{beta}/vigente")).GetProperty("id").GetGuid().Should().Be(idBeta);

        (await RemoverAsync(client, idAlfa)).Should().Be(await RemoverAsync(client, idBeta));
    }

    [Fact(DisplayName = "Um código que o código-fonte nunca viu percorre o ciclo de vida inteiro")]
    public async Task CodigoDesconhecido_PercorreOCicloDeVida()
    {
        // Nenhum arquivo de src/publicacoes/ conhece este código. Se acrescentar um
        // tipo exigisse alteração no domínio, este teste falharia.
        string codigo = CodigoUnico();

        using HttpClient client = _fixture.Factory.CreateClient();

        Guid id = await CriarAsync(client, codigo, congela: false, unico: false, irreversivel: false);

        (await LerAsync(client, $"{Base}/{id}")).GetProperty("codigo").GetString().Should().Be(codigo);
        (await LerAsync(client, $"{Base}/{codigo}/vigente")).GetProperty("id").GetGuid().Should().Be(id);

        using HttpRequestMessage put = new(HttpMethod.Put, new Uri($"{Admin}/{id}", UriKind.Relative));
        Autenticar(put);
        put.Content = JsonContent.Create(new
        {
            id,
            codigo,
            nome = "Renomeado sem que nada saiba o que ele é",
            congelaConfiguracao = true,
            unicoPorObjeto = true,
            efeitoIrreversivel = true,
            vigenciaInicio = "2026-01-01",
            vigenciaFim = (string?)null,
        });
        (await client.SendAsync(put)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        JsonElement atualizado = await LerAsync(client, $"{Base}/{id}");
        atualizado.GetProperty("congelaConfiguracao").GetBoolean().Should().BeTrue(
            "os atributos de consequência são dados: mudam por PUT, não por deploy");

        (await RemoverAsync(client, id)).Should().Be(HttpStatusCode.NoContent);
    }

    private static void Autenticar(HttpRequestMessage request)
    {
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
    }

    private static async Task<Guid> CriarAsync(
        HttpClient client, string codigo, bool congela, bool unico, bool irreversivel)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri(Admin, UriKind.Relative));
        Autenticar(request);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(new
        {
            codigo,
            nome = "Tipo de ato de teste",
            congelaConfiguracao = congela,
            unicoPorObjeto = unico,
            efeitoIrreversivel = irreversivel,
            vigenciaInicio = "2026-01-01",
            vigenciaFim = (string?)null,
        });

        HttpResponseMessage response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        return JsonSerializer.Deserialize<Guid>(await response.Content.ReadAsStringAsync());
    }

    private static async Task<JsonElement> LerAsync(HttpClient client, string url)
    {
        HttpResponseMessage response = await client.GetAsync(new Uri(url, UriKind.Relative));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }

    private static async Task<HttpStatusCode> RemoverAsync(HttpClient client, Guid id)
    {
        using HttpRequestMessage request = new(HttpMethod.Delete, new Uri($"{Admin}/{id}", UriKind.Relative));
        Autenticar(request);

        return (await client.SendAsync(request)).StatusCode;
    }

    private static IReadOnlyList<string> Nomes(JsonElement corpo) =>
        [.. corpo.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal)];

    private static string CodigoUnico()
    {
        string hex = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..12];
        return "PRINCIPIO_" + string.Concat(hex.Select(c => (char)('A' + Convert.ToInt32(c.ToString(), 16))));
    }
}
