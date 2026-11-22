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
public sealed class BancaDeOutroProcessoEndpointTests(CascadingFixture fixture)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Definir_BancaDeOutroProcesso_CriaNovaSemAlterarOriginal(bool etapa)
    {
        await CatalogosDoEnvelopeFechado.SemearTipoDeAtoResultadoPreliminarAsync(fixture.Factory);
        Guid destino = await SemearProcessoAsync(etapa);
        Guid origem = await SemearProcessoAsync(etapa);
        CatalogosDoEnvelopeFechado.CatalogosSemeados catalogoOrigem = await CatalogosDoEnvelopeFechado.SemearCatalogosRicosAsync(fixture.Factory, Guid.CreateVersion7().ToString("N")[^8..]);
        CatalogosDoEnvelopeFechado.CatalogosSemeados catalogoDestino = await CatalogosDoEnvelopeFechado.SemearCatalogosRicosAsync(fixture.Factory, Guid.CreateVersion7().ToString("N")[^8..]);
        using HttpClient cliente = fixture.Factory.CreateClient();
        JsonArray declaracaoOrigem = Declaracao(etapa, catalogoOrigem);
        JsonArray declaracaoDestino = Declaracao(etapa, catalogoDestino);
        Banca(declaracaoOrigem, etapa)["regraDeDeliberacao"]!["integrantes"] = 5;
        Banca(declaracaoOrigem, etapa)["regraDeDeliberacao"]!["quorum"] = 3;

        JsonNode respostaOrigem = await EnviarAsync(cliente, origem, declaracaoOrigem, etapa);
        Guid idOriginal = Banca(respostaOrigem, etapa)["id"]!.GetValue<Guid>();
        JsonNode bancaOriginalNoBanco = await LerBancaNoBancoAsync(origem, idOriginal, etapa);
        Banca(declaracaoDestino, etapa)["id"] = idOriginal;

        JsonNode respostaDestino = await EnviarAsync(cliente, destino, declaracaoDestino, etapa);
        ConferirResposta(respostaDestino, etapa, idOriginal, catalogoDestino.TipoBancaId);
        JsonNode.DeepEquals(bancaOriginalNoBanco, await LerBancaNoBancoAsync(origem, idOriginal, etapa)).Should().BeTrue();
    }

    private static void ConferirResposta(JsonNode resposta, bool etapa, Guid idOriginal, Guid tipoId)
    {
        JsonNode banca = Banca(resposta, etapa);
        banca["id"]!.GetValue<Guid>().Should().NotBeEmpty().And.NotBe(idOriginal);
        banca["tipoBancaOrigemId"]!.GetValue<Guid>().Should().Be(tipoId);
        banca["regraDeDeliberacao"]!["integrantes"]!.GetValue<int>().Should().Be(3);
        banca["regraDeDeliberacao"]!["quorum"]!.GetValue<int>().Should().Be(2);
        resposta.ToJsonString().Should().NotContain(idOriginal.ToString());
    }

    private async Task<Guid> SemearProcessoAsync(bool etapa)
    {
        if (etapa)
        {
            (Guid processoId, _) = await PublicacaoPeloEndpointDeTeste.SemearProcessoAsync(fixture.Factory, nameof(BancaDeOutroProcessoEndpointTests));
            return processoId;
        }

        return await SemearProcessoEmRascunhoAsync(fixture.Factory, nameof(BancaDeOutroProcessoEndpointTests), OrigemCandidatos.InscricaoPropria);
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

    private static async Task<JsonNode> EnviarAsync(HttpClient cliente, Guid processoId, JsonArray declaracao, bool etapa)
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
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, corpo);
        return JsonNode.Parse(corpo)!;
    }

    private async Task<JsonNode> LerBancaNoBancoAsync(Guid processoId, Guid bancaId, bool etapa)
    {
        await using AsyncServiceScope escopo = fixture.Factory.Services.CreateAsyncScope();
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
