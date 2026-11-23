namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.ApoioDoCronogramaDeFasesPeloEndpoint;

[Collection(CascadingCollection.Name)]
public sealed class ChavesDasFasesEEtapasEndpointTests(CascadingFixture ambienteDeTeste)
{
    [Fact]
    public async Task DefinirCronograma_FaseReconhecida_MantemChaveNaReleitura()
    {
        (Guid processoId, JsonArray fases) = await PrepararCronogramaAsync();
        using HttpClient cliente = ambienteDeTeste.Factory.CreateClient();
        JsonArray primeira = await GravarELerCronogramaAsync(cliente, processoId, fases);
        string chave = primeira[0]!["chave"]!.GetValue<string>();
        chave.Should().StartWith("fase_");
        fases[1]!["bancasRequeridas"]![0]!["id"] = primeira[1]!["bancasRequeridas"]![0]!["id"]!.DeepClone();
        fases[1]!["bancasRequeridas"]![1]!["id"] = primeira[1]!["bancasRequeridas"]![1]!["id"]!.DeepClone();

        JsonArray segunda = await GravarELerCronogramaAsync(cliente, processoId, fases);

        segunda[0]!["chave"]!.GetValue<string>().Should().Be(chave);
    }

    [Fact]
    public async Task DefinirEtapas_EtapaReconhecida_MantemChaveNaReleitura()
    {
        (Guid processoId, Guid tipoBancaId) = await PrepararEtapasAsync();
        using HttpClient cliente = ambienteDeTeste.Factory.CreateClient();
        JsonArray etapas = JsonSerializer.SerializeToNode(PayloadsDoEnvelopeFechado.EtapasIniciais(tipoBancaId))!.AsArray();
        etapas.RemoveAt(2);
        etapas.RemoveAt(1);
        etapas[0]!["faseCodigo"] = "RESULTADO_FINAL";
        JsonArray primeira = await GravarELerEtapasAsync(cliente, processoId, etapas);
        string chave = primeira[0]!["chave"]!.GetValue<string>();
        chave.Should().StartWith("etapa_");
        etapas[0]!["id"] = primeira[0]!["id"]!.DeepClone();

        JsonArray segunda = await GravarELerEtapasAsync(cliente, processoId, etapas);

        segunda[0]!["chave"]!.GetValue<string>().Should().Be(chave);
    }

    private async Task<(Guid ProcessoId, JsonArray Fases)> PrepararCronogramaAsync()
    {
        await CatalogosDoEnvelopeFechado.SemearTipoDeAtoResultadoPreliminarAsync(ambienteDeTeste.Factory);
        Guid processoId = await SemearProcessoEmRascunhoAsync(ambienteDeTeste.Factory, nameof(ChavesDasFasesEEtapasEndpointTests), OrigemCandidatos.InscricaoPropria);
        CatalogosDoEnvelopeFechado.CatalogosSemeados catalogos = await CatalogosDoEnvelopeFechado.SemearCatalogosRicosAsync(ambienteDeTeste.Factory, Guid.CreateVersion7().ToString("N")[^8..]);
        return (processoId, JsonSerializer.SerializeToNode(PayloadsDoEnvelopeFechado.CronogramaDeFases(catalogos))!.AsArray());
    }

    private async Task<(Guid ProcessoId, Guid TipoBancaId)> PrepararEtapasAsync()
    {
        (Guid processoId, _) = await PublicacaoPeloEndpointDeTeste.SemearProcessoAsync(ambienteDeTeste.Factory, nameof(ChavesDasFasesEEtapasEndpointTests));
        await CatalogosDoEnvelopeFechado.SemearTipoDeAtoResultadoPreliminarAsync(ambienteDeTeste.Factory);
        CatalogosDoEnvelopeFechado.CatalogosSemeados catalogos = await CatalogosDoEnvelopeFechado.SemearCatalogosRicosAsync(ambienteDeTeste.Factory, Guid.CreateVersion7().ToString("N")[^8..]);
        return (processoId, catalogos.TipoBancaId);
    }

    private static async Task<JsonArray> GravarELerCronogramaAsync(HttpClient cliente, Guid processoId, JsonArray fases)
    {
        using HttpResponseMessage resposta = await EnviarCronogramaComCorpoCruAsync(cliente, processoId, fases.ToJsonString());
        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent, await resposta.Content.ReadAsStringAsync());
        return (await LerProcessoComoJsonAsync(cliente, processoId))["cronogramaFases"]!.AsArray();
    }

    private static async Task<JsonArray> GravarELerEtapasAsync(HttpClient cliente, Guid processoId, JsonArray etapas)
    {
        using HttpResponseMessage resposta = await EnviarEtapasAsync(cliente, processoId, etapas);
        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent, await resposta.Content.ReadAsStringAsync());
        return (await LerProcessoComoJsonAsync(cliente, processoId))["etapas"]!.AsArray();
    }
    private static async Task<HttpResponseMessage> EnviarEtapasAsync(HttpClient cliente, Guid processoId, JsonArray etapas)
    {
        using HttpRequestMessage pedido = new(HttpMethod.Put, new Uri($"/api/selecao/processos-seletivos/{processoId}/etapas", UriKind.Relative))
        { Content = JsonContent.Create(etapas) };
        pedido.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        pedido.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
        pedido.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        return await cliente.SendAsync(pedido);
    }
}
