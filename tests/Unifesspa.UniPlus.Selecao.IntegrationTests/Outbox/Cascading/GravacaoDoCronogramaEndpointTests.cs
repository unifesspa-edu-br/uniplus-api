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
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.ApoioDoCronogramaDeFasesPeloEndpoint;

[Collection(CascadingCollection.Name)]
public sealed class GravacaoDoCronogramaEndpointTests(CascadingFixture ambienteDeTeste)
{
    [Fact]
    public async Task Definir_DuasBancasNovas_GravaIdentificadoresEPosicoesNaOrdem()
    {
        (Guid processoId, JsonArray fases, Guid tipoId) = await PrepararAsync();
        using HttpClient cliente = ambienteDeTeste.Factory.CreateClient();
        JsonArray bancas = fases[1]!["bancasRequeridas"]!.AsArray();
        bancas.RemoveAt(1);
        bancas.Add(bancas[0]!.DeepClone());
        bancas[1]!["regraDeDeliberacao"]!["integrantes"] = 5;
        bancas[1]!["regraDeDeliberacao"]!["quorum"] = 3;
        ConferirBancas(await GravarELerCronogramaAsync(cliente, processoId, fases), tipoId);
    }

    [Fact]
    public async Task Definir_DataComSeteCasas_GravaComPrecisaoDeMicrossegundo()
    {
        (Guid processoId, JsonArray fases, _) = await PrepararAsync();
        using HttpClient cliente = ambienteDeTeste.Factory.CreateClient();
        fases[0]!["inicio"] = "2027-01-25T11:00:00.1234567Z";
        fases[0]!["fim"] = "2027-01-27T11:00:00.7654321Z";
        JsonNode leitura = await GravarELerCronogramaAsync(cliente, processoId, fases);
        leitura[0]!["cronograma"]!["inicio"]!.GetValue<DateTimeOffset>()
            .Should().Be(new DateTimeOffset(2027, 1, 25, 11, 0, 0, TimeSpan.Zero).AddTicks(1_234_560));
    }

    [Fact]
    public async Task Definir_PrazoDaSegundaInstanciaComCincoCasas_RecusaPorFormato()
    {
        (Guid processoId, JsonArray fases, _) = await PrepararAsync();
        using HttpClient cliente = ambienteDeTeste.Factory.CreateClient();
        fases[1]!["regraRecurso"] = PayloadsDoEnvelopeFechado.RegraDeRecursoAncoradaNoResultado();
        fases[1]!["regraRecurso"]!["prazoSegundaInstanciaValor"] = 2.12345m;
        using HttpResponseMessage resposta = await EnviarCronogramaComCorpoCruAsync(cliente, processoId, fases.ToJsonString());
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await resposta.Content.ReadAsStringAsync()).Should().Contain("uniplus.validacao.formato");
    }

    [Fact]
    public async Task Definir_PrazosComQuatroCasas_GravaOsValoresDeclarados()
    {
        (Guid processoId, JsonArray fases, _) = await PrepararAsync();
        using HttpClient cliente = ambienteDeTeste.Factory.CreateClient();
        fases[1]!["regraRecurso"] = PayloadsDoEnvelopeFechado.RegraDeRecursoAncoradaNoResultado();
        JsonNode leitura = await GravarELerCronogramaAsync(cliente, processoId, fases);
        JsonNode parametros = leitura[1]!["publicacoes"]!["regraRecurso"]!["parametros"]!;
        parametros["prazoValor"]!.GetValue<decimal>().Should().Be(1.1m);
        parametros["prazoSegundaInstanciaValor"]!.GetValue<decimal>().Should().Be(2.1234m);
    }

    [Fact]
    public async Task Definir_BancaExistente_MantemIdentificadorECopiaDoTipo()
    {
        (Guid processoId, JsonArray fases, Guid tipoId) = await PrepararAsync();
        using HttpClient cliente = ambienteDeTeste.Factory.CreateClient();
        JsonNode anterior = (await GravarELerCronogramaAsync(cliente, processoId, fases))[1]!["bancasRequeridas"]![0]!.DeepClone();
        fases[1]!["bancasRequeridas"]![0]!["id"] = anterior["id"]!.DeepClone();
        await AlterarNomeDoTipoAsync(tipoId, "Nome alterado para teste");
        try
        {
            JsonNode atual = (await GravarELerCronogramaAsync(cliente, processoId, fases))[1]!["bancasRequeridas"]![0]!;
            JsonNode.DeepEquals(anterior, atual).Should().BeTrue();
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

    private async Task<(Guid ProcessoId, JsonArray Fases, Guid TipoId)> PrepararAsync()
    {
        await CatalogosDoEnvelopeFechado.SemearTipoDeAtoResultadoPreliminarAsync(ambienteDeTeste.Factory);
        Guid processoId = await SemearProcessoEmRascunhoAsync(ambienteDeTeste.Factory, nameof(GravacaoDoCronogramaEndpointTests), OrigemCandidatos.InscricaoPropria);
        CatalogosDoEnvelopeFechado.CatalogosSemeados catalogos = await CatalogosDoEnvelopeFechado.SemearCatalogosRicosAsync(ambienteDeTeste.Factory, Guid.CreateVersion7().ToString("N")[^8..]);
        JsonArray fases = JsonSerializer.SerializeToNode(PayloadsDoEnvelopeFechado.CronogramaDeFases(catalogos))!.AsArray();
        return (processoId, fases, catalogos.TipoBancaId);
    }

    private async Task AlterarNomeDoTipoAsync(Guid tipoId, string nome)
    {
        await using AsyncServiceScope scope = ambienteDeTeste.Factory.Services.CreateAsyncScope();
        UniPlusDbContext banco = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        TipoBanca tipo = await banco.TiposBanca.SingleAsync(item => item.Id == tipoId);
        tipo.Atualizar(new DeclaracaoDoTipoDeBanca(nome, "RESULTADO_FINAL", Papel: "AVALIA_O_MERITO", TiposDeDecisaoJulgados: ["ANALISE_DOCUMENTAL"])).IsSuccess.Should().BeTrue();
        await banco.SaveChangesAsync();
    }

    private static async Task<JsonNode> GravarELerCronogramaAsync(HttpClient cliente, Guid processoId, JsonArray fases)
    {
        using HttpResponseMessage resposta = await EnviarCronogramaComCorpoCruAsync(cliente, processoId, fases.ToJsonString());
        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent, await resposta.Content.ReadAsStringAsync());
        return (await LerProcessoComoJsonAsync(cliente, processoId))["cronogramaFases"]!;
    }
}
