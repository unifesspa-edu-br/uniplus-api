namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

[Collection(CascadingCollection.Name)]
public sealed class DeliberacaoDaBancaEndpointTests(CascadingFixture fixture)
{
    [Fact(DisplayName = "A banca recusa modo desconhecido sem repetir o valor e aceita o modo válido")]
    public async Task DefinirEtapas_ModoDeDeliberacao_ValidaCodigoSemRepetirValor()
    {
        (Guid processoId, _) = await PublicacaoPeloEndpointDeTeste.SemearProcessoAsync(fixture.Factory, nameof(DeliberacaoDaBancaEndpointTests));
        CatalogosDoEnvelopeFechado.CatalogosSemeados catalogos = await CatalogosDoEnvelopeFechado.SemearCatalogosRicosAsync(fixture.Factory, Guid.CreateVersion7().ToString("N")[..8]);
        using HttpClient client = fixture.Factory.CreateClient();
        JsonArray etapas = Etapas(catalogos.TipoBancaId);
        JsonNode regra = etapas[1]!["bancas"]![0]!["regraDeDeliberacao"]!;
        regra["modo"] = "colegiado";
        using HttpResponseMessage invalida = await EnviarAsync(client, processoId, etapas);
        await ConferirRecusaAsync(invalida);
        regra["modo"] = "colegiadaComVotacao";
        using HttpResponseMessage valida = await EnviarAsync(client, processoId, etapas);
        valida.StatusCode.Should().Be(HttpStatusCode.NoContent, await valida.Content.ReadAsStringAsync());
    }

    private static JsonArray Etapas(Guid tipoBancaId)
    {
        JsonArray etapas = JsonSerializer.SerializeToNode(PayloadsDoEnvelopeFechado.EtapasIniciais(tipoBancaId))!.AsArray();
        foreach (JsonNode? etapa in etapas)
        {
            etapa!["faseCodigo"] = "RESULTADO_FINAL";
        }

        return etapas;
    }

    private static async Task ConferirRecusaAsync(HttpResponseMessage resposta)
    {
        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, corpo);
        corpo.Should().NotContain("colegiado");
        JsonElement problema = JsonSerializer.Deserialize<JsonElement>(corpo);
        problema.GetProperty("code").GetString().Should().Be("uniplus.selecao.banca.modo_de_deliberacao_invalido");
        problema.GetProperty("errors").EnumerateArray().Select(e => e.GetProperty("field").GetString())
            .Should().Contain("etapas[1].bancas[0].regraDeDeliberacao.modo");
    }

    private static async Task<HttpResponseMessage> EnviarAsync(HttpClient client, Guid processoId, JsonArray etapas)
    {
        using HttpRequestMessage request = new(HttpMethod.Put, new Uri($"/api/selecao/processos-seletivos/{processoId}/etapas", UriKind.Relative))
        { Content = JsonContent.Create(etapas) };
        request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        return await client.SendAsync(request);
    }
}
