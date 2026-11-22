namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

[Collection(CascadingCollection.Name)]
public sealed class RespostaDasEtapasGravadasEndpointTests(CascadingFixture fixture)
{
    [Fact]
    public async Task Definir_MesmaChave_DevolveMesmoCorpoDaLeitura()
    {
        (Guid processoId, _) = await PublicacaoPeloEndpointDeTeste.SemearProcessoAsync(fixture.Factory, nameof(RespostaDasEtapasGravadasEndpointTests));
        using HttpClient cliente = fixture.Factory.CreateClient();
        string chave = Guid.CreateVersion7().ToString("N");
        using HttpResponseMessage primeira = await EnviarAsync(cliente, processoId, chave, "plataforma-admin");
        using HttpResponseMessage repetida = await EnviarAsync(cliente, processoId, chave, "plataforma-admin");
        primeira.StatusCode.Should().Be(HttpStatusCode.OK);
        repetida.StatusCode.Should().Be(HttpStatusCode.OK);
        string corpo = await primeira.Content.ReadAsStringAsync();
        (await repetida.Content.ReadAsStringAsync()).Should().Be(corpo);
        JsonNode? leitura = await LerEtapasAsync(cliente, processoId);
        JsonNode.DeepEquals(JsonNode.Parse(corpo), leitura).Should().BeTrue();
    }

    [Fact]
    public async Task Definir_BancaNova_DevolveIdentidadeEValoresGravados()
    {
        (Guid processoId, _) = await PublicacaoPeloEndpointDeTeste.SemearProcessoAsync(fixture.Factory, nameof(Definir_BancaNova_DevolveIdentidadeEValoresGravados));
        CatalogosDoEnvelopeFechado.CatalogosSemeados catalogos = await CatalogosDoEnvelopeFechado.SemearCatalogosRicosAsync(fixture.Factory, Guid.CreateVersion7().ToString("N")[..8]);
        using HttpClient cliente = fixture.Factory.CreateClient();
        JsonArray etapas = EtapasComBanca(catalogos.TipoBancaId);
        using HttpResponseMessage resposta = await EnviarEtapasAsync(cliente, processoId, etapas);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
        JsonNode corpo = JsonNode.Parse(await resposta.Content.ReadAsStringAsync())!;
        JsonNode banca = corpo[1]!["bancas"]![0]!;
        Guid.Parse(banca["id"]!.GetValue<string>()).Should().NotBeEmpty();
        banca["tipoBancaOrigemId"]!.GetValue<Guid>().Should().Be(catalogos.TipoBancaId);
        banca["posicaoNoTipo"]!.GetValue<int>().Should().Be(1);
        JsonNode.DeepEquals(corpo, await LerEtapasAsync(cliente, processoId)).Should().BeTrue();
    }

    private static JsonArray EtapasComBanca(Guid tipoBancaId)
    {
        JsonArray etapas = JsonSerializer.SerializeToNode(PayloadsDoEnvelopeFechado.EtapasIniciais(tipoBancaId))!.AsArray();
        foreach (JsonNode? etapa in etapas)
        {
            etapa!["faseCodigo"] = "RESULTADO_FINAL";
        }

        return etapas;
    }

    private static async Task<HttpResponseMessage> EnviarEtapasAsync(HttpClient cliente, Guid processoId, JsonArray etapas)
    {
        using HttpRequestMessage pedido = new(HttpMethod.Put, new Uri($"/api/selecao/processos-seletivos/{processoId}/etapas", UriKind.Relative))
        {
            Content = JsonContent.Create(etapas),
        };
        Autenticar(pedido, "plataforma-admin");
        pedido.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        return await cliente.SendAsync(pedido);
    }

    [Fact]
    public async Task Definir_SemPermissao_RecusaSemDadosDoProcesso()
    {
        (Guid processoId, _) = await PublicacaoPeloEndpointDeTeste.SemearProcessoAsync(fixture.Factory, nameof(Definir_SemPermissao_RecusaSemDadosDoProcesso));
        using HttpClient cliente = fixture.Factory.CreateClient();
        using HttpResponseMessage resposta = await EnviarAsync(cliente, processoId, Guid.CreateVersion7().ToString("N"), "candidato");
        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await resposta.Content.ReadAsStringAsync()).Should().NotContain("etapas");
    }

    private static async Task<HttpResponseMessage> EnviarAsync(HttpClient cliente, Guid processoId, string chave, string papel)
    {
        using HttpRequestMessage pedido = new(HttpMethod.Put, new Uri($"/api/selecao/processos-seletivos/{processoId}/etapas", UriKind.Relative))
        {
            Content = JsonContent.Create(Array.Empty<object>()),
        };
        Autenticar(pedido, papel);
        pedido.Headers.TryAddWithoutValidation("Idempotency-Key", chave);
        return await cliente.SendAsync(pedido);
    }

    private static async Task<JsonNode?> LerEtapasAsync(HttpClient cliente, Guid processoId)
    {
        using HttpRequestMessage pedido = new(HttpMethod.Get, new Uri($"/api/selecao/processos-seletivos/{processoId}", UriKind.Relative));
        Autenticar(pedido, "plataforma-admin");
        using HttpResponseMessage resposta = await cliente.SendAsync(pedido);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonNode? processo = JsonNode.Parse(await resposta.Content.ReadAsStringAsync());
        return processo!["etapas"];
    }

    private static void Autenticar(HttpRequestMessage pedido, string papel)
    {
        pedido.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        pedido.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, papel);
    }
}
