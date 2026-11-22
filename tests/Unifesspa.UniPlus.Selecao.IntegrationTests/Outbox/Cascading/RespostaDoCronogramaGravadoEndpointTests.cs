namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.ApoioDoCronogramaDeFasesPeloEndpoint;

[Collection(CascadingCollection.Name)]
public sealed class RespostaDoCronogramaGravadoEndpointTests(CascadingFixture fixture)
{
    [Fact]
    public async Task Definir_DuasBancasNovas_DevolveIdentificadoresEPosicoesNaOrdem()
    {
        (HttpClient cliente, Guid processoId, JsonArray fases, Guid tipoId) = await PrepararAsync();
        JsonArray bancas = fases[1]!["bancasRequeridas"]!.AsArray();
        bancas.RemoveAt(1);
        bancas.Add(bancas[0]!.DeepClone());
        bancas[1]!["regraDeDeliberacao"]!["integrantes"] = 5;
        bancas[1]!["regraDeDeliberacao"]!["quorum"] = 3;
        using HttpResponseMessage resposta = await EnviarAsync(cliente, processoId, fases);
        JsonNode corpo = await CorpoDeSucessoAsync(resposta);
        ConferirBancas(corpo, tipoId);
        JsonNode.DeepEquals(corpo, await LerCronogramaAsync(cliente, processoId)).Should().BeTrue();
    }

    [Fact]
    public async Task Definir_DataComSeteCasas_DevolveOMesmoCorpoDaLeitura()
    {
        (HttpClient cliente, Guid processoId, JsonArray fases, _) = await PrepararAsync();
        using (cliente)
        {
            fases[0]!["inicio"] = "2027-01-25T11:00:00.1234567Z";
            fases[0]!["fim"] = "2027-01-27T11:00:00.7654321Z";
            using HttpResponseMessage resposta = await EnviarAsync(cliente, processoId, fases);
            JsonNode corpo = await CorpoDeSucessoAsync(resposta);
            corpo[0]!["cronograma"]!["inicio"]!.GetValue<DateTimeOffset>()
                .Should().Be(new DateTimeOffset(2027, 1, 25, 11, 0, 0, TimeSpan.Zero).AddTicks(1_234_560));
            JsonNode.DeepEquals(corpo, await LerCronogramaAsync(cliente, processoId)).Should().BeTrue();
        }
    }

    [Fact]
    public async Task Definir_BancaExistente_MantemIdentificadorECopiaDoTipo()
    {
        (HttpClient cliente, Guid processoId, JsonArray fases, Guid tipoId) = await PrepararAsync();
        using HttpResponseMessage primeira = await EnviarAsync(cliente, processoId, fases);
        JsonNode anterior = (await CorpoDeSucessoAsync(primeira))[1]!["bancasRequeridas"]![0]!.DeepClone();
        fases[1]!["bancasRequeridas"]![0]!["id"] = anterior["id"]!.DeepClone();
        await AlterarNomeDoTipoAsync(tipoId, "Nome alterado para teste");
        try
        {
            using HttpResponseMessage segunda = await EnviarAsync(cliente, processoId, fases);
            JsonNode corpo = await CorpoDeSucessoAsync(segunda);
            JsonNode atual = corpo[1]!["bancasRequeridas"]![0]!;
            JsonNode.DeepEquals(anterior, atual).Should().BeTrue();
            JsonNode.DeepEquals(corpo, await LerCronogramaAsync(cliente, processoId)).Should().BeTrue();
        }
        finally { await AlterarNomeDoTipoAsync(tipoId, anterior["nome"]!.GetValue<string>()); }
    }

    private static void ConferirBancas(JsonNode corpo, Guid tipoId)
    {
        JsonArray gravadas = corpo[1]!["bancasRequeridas"]!.AsArray();
        Guid primeiro = gravadas[0]!["id"]!.GetValue<Guid>();
        Guid segundo = gravadas[1]!["id"]!.GetValue<Guid>();
        primeiro.Should().NotBeEmpty().And.NotBe(segundo);
        segundo.Should().NotBeEmpty();
        gravadas[0]!["tipoBancaOrigemId"]!.GetValue<Guid>().Should().Be(tipoId);
        gravadas[1]!["tipoBancaOrigemId"]!.GetValue<Guid>().Should().Be(tipoId);
        gravadas[0]!["regraDeDeliberacao"]!["integrantes"]!.GetValue<int>().Should().Be(3);
        gravadas[1]!["regraDeDeliberacao"]!["integrantes"]!.GetValue<int>().Should().Be(5);
        gravadas[0]!["posicaoNoTipo"]!.GetValue<int>().Should().Be(1);
        gravadas[1]!["posicaoNoTipo"]!.GetValue<int>().Should().Be(2);
    }

    private async Task<(HttpClient Cliente, Guid ProcessoId, JsonArray Fases, Guid TipoId)> PrepararAsync()
    {
        await CatalogosDoEnvelopeFechado.SemearTipoDeAtoResultadoPreliminarAsync(fixture.Factory);
        Guid processoId = await SemearProcessoEmRascunhoAsync(fixture.Factory, nameof(RespostaDoCronogramaGravadoEndpointTests), OrigemCandidatos.InscricaoPropria);
        CatalogosDoEnvelopeFechado.CatalogosSemeados catalogos = await CatalogosDoEnvelopeFechado.SemearCatalogosRicosAsync(fixture.Factory, Guid.CreateVersion7().ToString("N")[^8..]);
        JsonArray fases = JsonSerializer.SerializeToNode(PayloadsDoEnvelopeFechado.CronogramaDeFases(catalogos))!.AsArray();
        return (fixture.Factory.CreateClient(), processoId, fases, catalogos.TipoBancaId);
    }

    private async Task AlterarNomeDoTipoAsync(Guid tipoId, string nome)
    {
        await using AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext banco = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        TipoBanca tipo = await banco.TiposBanca.SingleAsync(item => item.Id == tipoId);
        tipo.Atualizar(new DeclaracaoDoTipoDeBanca(nome, "RESULTADO_FINAL", Papel: "AVALIA_O_MERITO", TiposDeDecisaoJulgados: ["ANALISE_DOCUMENTAL"])).IsSuccess.Should().BeTrue();
        await banco.SaveChangesAsync();
    }

    private static Task<HttpResponseMessage> EnviarAsync(HttpClient cliente, Guid processoId, JsonArray fases) =>
        EnviarCronogramaComCorpoCruAsync(cliente, processoId, fases.ToJsonString());

    private static async Task<JsonNode> CorpoDeSucessoAsync(HttpResponseMessage resposta)
    {
        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, corpo);
        return JsonNode.Parse(corpo)!;
    }

    private static async Task<JsonNode?> LerCronogramaAsync(HttpClient cliente, Guid processoId)
    {
        using HttpRequestMessage pedido = new(HttpMethod.Get, new Uri($"/api/selecao/processos-seletivos/{processoId}", UriKind.Relative));
        Autenticar(pedido);
        using HttpResponseMessage resposta = await cliente.SendAsync(pedido);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        return JsonNode.Parse(await resposta.Content.ReadAsStringAsync())!["cronogramaFases"];
    }
}
