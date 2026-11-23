namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.ApoioDoCronogramaDeFasesPeloEndpoint;

[Collection(CascadingCollection.Name)]
public sealed class BancaDeOutroProcessoEndpointTests(CascadingFixture ambienteDeTeste)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Definir_BancaDeOutroProcesso_CriaNovaSemAlterarOriginal(bool etapa)
    {
        using HttpClient cliente = ambienteDeTeste.Factory.CreateClient();
        (Guid origem, Guid idOriginal) = await GravarBancaOriginalAsync(cliente, etapa);
        JsonNode bancaOriginal = await LerBancaNoBancoAsync(origem, idOriginal, etapa);
        (Guid destino, Guid tipoId) = await GravarCopiaDaBancaEmOutroProcessoAsync(cliente, etapa, idOriginal);
        JsonNode processoLido = await LerProcessoComoJsonAsync(cliente, destino);
        ConferirBancaNova(processoLido, etapa, idOriginal, tipoId);
        JsonNode.DeepEquals(bancaOriginal, await LerBancaNoBancoAsync(origem, idOriginal, etapa)).Should().BeTrue();
    }

    private async Task<(Guid ProcessoId, Guid BancaId)> GravarBancaOriginalAsync(HttpClient cliente, bool etapa)
    {
        await CatalogosDoEnvelopeFechado.SemearTipoDeAtoResultadoPreliminarAsync(ambienteDeTeste.Factory);
        Guid processoId = await SemearProcessoAsync(etapa);
        CatalogosDoEnvelopeFechado.CatalogosSemeados catalogos = await CatalogosDoEnvelopeFechado.SemearCatalogosRicosAsync(ambienteDeTeste.Factory, Guid.CreateVersion7().ToString("N")[^8..]);
        JsonArray declaracao = Declaracao(etapa, catalogos);
        Banca(declaracao, etapa)["regraDeDeliberacao"]!["integrantes"] = 5;
        Banca(declaracao, etapa)["regraDeDeliberacao"]!["quorum"] = 3;
        await GravarAsync(cliente, processoId, declaracao, etapa);
        JsonNode processo = await LerProcessoComoJsonAsync(cliente, processoId);
        return (processoId, Banca(processo[etapa ? "etapas" : "cronogramaFases"]!, etapa)["id"]!.GetValue<Guid>());
    }

    private async Task<(Guid ProcessoId, Guid TipoBancaId)> GravarCopiaDaBancaEmOutroProcessoAsync(HttpClient cliente, bool etapa, Guid idOriginal)
    {
        Guid processoId = await SemearProcessoAsync(etapa);
        CatalogosDoEnvelopeFechado.CatalogosSemeados catalogos = await CatalogosDoEnvelopeFechado.SemearCatalogosRicosAsync(ambienteDeTeste.Factory, Guid.CreateVersion7().ToString("N")[^8..]);
        JsonArray declaracao = Declaracao(etapa, catalogos);
        Banca(declaracao, etapa)["id"] = idOriginal;
        await GravarAsync(cliente, processoId, declaracao, etapa);
        return (processoId, catalogos.TipoBancaId);
    }

    private static void ConferirBancaNova(JsonNode processoLido, bool etapa, Guid idOriginal, Guid tipoId)
    {
        JsonNode banca = Banca(processoLido[etapa ? "etapas" : "cronogramaFases"]!, etapa);
        banca["id"]!.GetValue<Guid>().Should().NotBeEmpty().And.NotBe(idOriginal);
        banca["tipoBancaOrigemId"]!.GetValue<Guid>().Should().Be(tipoId);
        banca["regraDeDeliberacao"]!["integrantes"]!.GetValue<int>().Should().Be(3);
        banca["regraDeDeliberacao"]!["quorum"]!.GetValue<int>().Should().Be(2);
        processoLido.ToJsonString().Should().NotContain(idOriginal.ToString());
    }

    private async Task<Guid> SemearProcessoAsync(bool etapa)
    {
        if (etapa)
        {
            (Guid processoId, _) = await PublicacaoPeloEndpointDeTeste.SemearProcessoAsync(ambienteDeTeste.Factory, nameof(BancaDeOutroProcessoEndpointTests));
            return processoId;
        }

        return await SemearProcessoEmRascunhoAsync(ambienteDeTeste.Factory, nameof(BancaDeOutroProcessoEndpointTests), OrigemCandidatos.InscricaoPropria);
    }

    private static JsonArray Declaracao(bool etapa, CatalogosDoEnvelopeFechado.CatalogosSemeados catalogos)
    {
        if (!etapa)
        {
            JsonArray fases = JsonSerializer.SerializeToNode(PayloadsDoEnvelopeFechado.CronogramaDeFases(catalogos))!.AsArray();
            fases[1]!["bancasRequeridas"]!.AsArray().RemoveAt(1);
            return fases;
        }

        JsonArray etapas = JsonSerializer.SerializeToNode(PayloadsDoEnvelopeFechado.EtapasIniciais(catalogos.TipoBancaId))!.AsArray();
        foreach (JsonNode? item in etapas)
        {
            item!["faseCodigo"] = "RESULTADO_FINAL";
        }

        return etapas;
    }

    private static JsonNode Banca(JsonNode declaracao, bool etapa) => declaracao[1]![etapa ? "bancas" : "bancasRequeridas"]![0]!;

    private static async Task GravarAsync(HttpClient cliente, Guid processoId, JsonArray declaracao, bool etapa)
    {
        using HttpRequestMessage pedido = new(HttpMethod.Put, new Uri($"/api/selecao/processos-seletivos/{processoId}/{(etapa ? "etapas" : "cronograma-fases")}", UriKind.Relative))
        {
            Content = JsonContent.Create(declaracao),
        };
        pedido.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        pedido.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
        pedido.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        using HttpResponseMessage resposta = await cliente.SendAsync(pedido);
        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent, corpo);
    }

    private async Task<JsonNode> LerBancaNoBancoAsync(Guid processoId, Guid bancaId, bool etapa)
    {
        await using AsyncServiceScope escopo = ambienteDeTeste.Factory.Services.CreateAsyncScope();
        UniPlusDbContext banco = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        if (etapa)
        {
            List<EtapaProcesso> etapas = await banco.EtapasProcesso.AsNoTracking().Include(item => item.Bancas)
                .Where(item => item.ProcessoSeletivoId == processoId).ToListAsync();
            return JsonSerializer.SerializeToNode(etapas.SelectMany(item => item.Bancas).Single(item => item.Id == bancaId))!;
        }

        List<FaseCronograma> fases = await banco.FasesCronograma.AsNoTracking().Include(item => item.BancasRequeridas)
            .ThenInclude(item => item.CandidatosAtendidos)
            .Where(item => item.ProcessoSeletivoId == processoId).ToListAsync();
        return JsonSerializer.SerializeToNode(fases.SelectMany(item => item.BancasRequeridas).Single(item => item.Id == bancaId))!;
    }
}
