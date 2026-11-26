namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.ApoioDoCronogramaDeFasesPeloEndpoint;

[Collection(CascadingCollection.Name)]
public sealed class ChavesDasBancasPeloEndpointTests(CascadingFixture ambienteDeTeste)
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Gravar_BancasDosDoisBlocos_UsaChavesDistintas(bool fasePrimeiro)
    {
        (Guid processoId, JsonArray fases, JsonArray etapas) = await PrepararAsync();
        using HttpClient cliente = ambienteDeTeste.Factory.CreateClient();
        await GravarOsBlocosAsync(cliente, processoId, fases, etapas, fasePrimeiro);
        (string fase, string etapa) = await LerChavesAsync(processoId);

        fase.Should().Be(fasePrimeiro ? "banca_analise_documental" : "banca_analise_documental_2");
        etapa.Should().Be(fasePrimeiro ? "banca_analise_documental_2" : "banca_analise_documental");
    }

    [Fact]
    public async Task Gravar_BancasReconhecidas_MantemAsChaves()
    {
        (Guid processoId, JsonArray fases, JsonArray etapas) = await PrepararAsync();
        using HttpClient cliente = ambienteDeTeste.Factory.CreateClient();
        await GravarOsBlocosAsync(cliente, processoId, fases, etapas, true);
        (string fase, string etapa) = await LerChavesAsync(processoId);
        JsonNode processo = await LerProcessoComoJsonAsync(cliente, processoId);
        DevolverIdentificadores(fases, etapas, processo);

        await GravarEtapasAsync(cliente, processoId, etapas);
        await GravarFasesAsync(cliente, processoId, fases);

        (await LerChavesAsync(processoId)).Should().Be((fase, etapa));
    }

    [Fact]
    public async Task Gravar_ConsequenciaDaAusencia_DevolveEPreservaOuApaga()
    {
        (Guid processoId, JsonArray fases, JsonArray etapas) = await PrepararAsync();
        using HttpClient cliente = ambienteDeTeste.Factory.CreateClient();
        fases[1]!["bancasRequeridas"]![0]!["consequenciaDaAusencia"] = "ELIMINA_DO_PROCESSO";
        etapas[0]!["bancas"]![0]!["consequenciaDaAusencia"] = "ELIMINA_DO_PROCESSO";
        await GravarOsBlocosAsync(cliente, processoId, fases, etapas, true);
        JsonNode processo = await LerProcessoComoJsonAsync(cliente, processoId);
        DevolverIdentificadores(fases, etapas, processo);
        await GravarOsBlocosAsync(cliente, processoId, fases, etapas, true);
        ConferirConsequencias(await LerProcessoComoJsonAsync(cliente, processoId), "ELIMINA_DO_PROCESSO");
        fases[1]!["bancasRequeridas"]![0]!.AsObject().Remove("consequenciaDaAusencia");
        etapas[0]!["bancas"]![0]!.AsObject().Remove("consequenciaDaAusencia");
        await GravarOsBlocosAsync(cliente, processoId, fases, etapas, true);
        ConferirConsequencias(await LerProcessoComoJsonAsync(cliente, processoId), null);
    }

    private static void ConferirConsequencias(JsonNode processo, string? codigo)
    {
        JsonNode? fase = processo["cronogramaFases"]![1]!["bancasRequeridas"]!.AsArray()
            .Single(item => item!["codigo"]!["codigo"]!.ToString() == TipoDeBanca.AnaliseDocumental.Codigo());
        string? consequenciaDaFase = fase!["consequenciaDaAusencia"]?["codigo"]?.ToString();
        string? consequenciaDaEtapa = processo["etapas"]![0]!["bancas"]![0]!["consequenciaDaAusencia"]?["codigo"]?.ToString();
        consequenciaDaFase.Should().Be(codigo);
        consequenciaDaEtapa.Should().Be(codigo);
    }

    private static void DevolverIdentificadores(JsonArray fases, JsonArray etapas, JsonNode processo)
    {
        JsonNode fase = processo["cronogramaFases"]![1]!;
        foreach (JsonNode? declarada in fases[1]!["bancasRequeridas"]!.AsArray())
        {
            JsonNode gravada = fase["bancasRequeridas"]!.AsArray().Single(item => item!["tipoBancaOrigemId"]!.ToString() == declarada!["tipoBancaId"]!.ToString())!;
            declarada!["id"] = gravada["id"]!.DeepClone();
        }

        JsonNode etapa = processo["etapas"]![0]!;
        etapas[0]!["id"] = etapa["id"]!.DeepClone();
        etapas[0]!["bancas"]![0]!["id"] = etapa["bancas"]![0]!["id"]!.DeepClone();
    }

    private async Task<(Guid, JsonArray, JsonArray)> PrepararAsync()
    {
        await CatalogosDoEnvelopeFechado.SemearTipoDeAtoResultadoPreliminarAsync(ambienteDeTeste.Factory);
        Guid processoId = await SemearProcessoEmRascunhoAsync(ambienteDeTeste.Factory, nameof(ChavesDasBancasPeloEndpointTests), OrigemCandidatos.InscricaoPropria);
        CatalogosDoEnvelopeFechado.CatalogosSemeados catalogos = await CatalogosDoEnvelopeFechado.SemearCatalogosRicosAsync(ambienteDeTeste.Factory, Guid.CreateVersion7().ToString("N")[^8..]);
        JsonArray fases = JsonSerializer.SerializeToNode(PayloadsDoEnvelopeFechado.CronogramaDeFases(catalogos))!.AsArray();
        JsonArray etapas = JsonSerializer.SerializeToNode(PayloadsDoEnvelopeFechado.EtapasIniciais(catalogos.TipoBancaId))!.AsArray();
        etapas.RemoveAt(2);
        etapas.RemoveAt(0);
        return (processoId, fases, etapas);
    }

    private static async Task GravarOsBlocosAsync(HttpClient cliente, Guid processoId, JsonArray fases, JsonArray etapas, bool fasePrimeiro)
    {
        if (fasePrimeiro)
        {
            await GravarFasesAsync(cliente, processoId, fases);
            await GravarEtapasAsync(cliente, processoId, etapas);
            return;
        }

        JsonArray fasesSemBancas = fases.DeepClone().AsArray();
        fasesSemBancas[1]!["bancasRequeridas"] = new JsonArray();
        await GravarFasesAsync(cliente, processoId, fasesSemBancas);
        await GravarEtapasAsync(cliente, processoId, etapas);
        await GravarFasesAsync(cliente, processoId, fases);
    }

    private static async Task GravarFasesAsync(HttpClient cliente, Guid processoId, JsonArray fases)
    {
        using HttpResponseMessage resposta = await EnviarCronogramaComCorpoCruAsync(cliente, processoId, fases.ToJsonString());
        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent, await resposta.Content.ReadAsStringAsync());
    }

    private static async Task GravarEtapasAsync(HttpClient cliente, Guid processoId, JsonArray etapas)
    {
        using HttpRequestMessage pedido = new(HttpMethod.Put, new Uri($"/api/selecao/processos-seletivos/{processoId}/etapas", UriKind.Relative))
        { Content = JsonContent.Create(etapas) };
        Autenticar(pedido);
        pedido.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        using HttpResponseMessage resposta = await cliente.SendAsync(pedido);
        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent, await resposta.Content.ReadAsStringAsync());
    }

    private async Task<(string, string)> LerChavesAsync(Guid processoId)
    {
        await using AsyncServiceScope escopo = ambienteDeTeste.Factory.Services.CreateAsyncScope();
        UniPlusDbContext contexto = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        ProcessoSeletivo processo = await contexto.Set<ProcessoSeletivo>().Include(item => item.CronogramaFases).ThenInclude(item => item.BancasRequeridas)
            .Include(item => item.Etapas).ThenInclude(item => item.Bancas).SingleAsync(item => item.Id == processoId);
        return (processo.CronogramaFases.SelectMany(item => item.BancasRequeridas).Single(item => item.Tipo.Codigo == TipoDeBanca.AnaliseDocumental).Chave.Valor, processo.Etapas.Single().Bancas.Single().Chave.Valor);
    }
}
