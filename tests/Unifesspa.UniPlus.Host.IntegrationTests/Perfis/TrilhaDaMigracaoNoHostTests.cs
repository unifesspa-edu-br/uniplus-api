namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;

/// <summary>
/// A trilha da concessão lida pela API depois da revisão das concessões: a decisão, o ator sistema na revogação pela
/// migração, a referência à revisão e a autoria de quem decidiu.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
public sealed class TrilhaDaMigracaoNoHostTests : RevisaoDasConcessoesNoHostTestBase
{
    private static readonly TimeSpan DepoisDoPrazo = TimeSpan.FromHours(73);

    public TrilhaDaMigracaoNoHostTests(MonolitoDaInstalacaoFixture fixture, HostDaRevisaoDasConcessoes hostDaClasse)
        : base(fixture, hostDaClasse)
    {
    }

    [Fact(DisplayName = "A trilha da concessão migrada mostra a decisão humana com a revisão, e a da retida mostra a retenção do sistema sem o autor do envio")]
    public async Task Trilha_DaMigracaoAprovada()
    {
        RevisaoAberta cenario = await AbrirRevisaoAsync(Isabella, Leandro);
        Guid migrada = cenario.Concessoes[Leandro];
        Guid propria = cenario.Concessoes[Isabella];
        using HttpClient isabella = await GestaoAsync();
        using HttpClient diogo = await GestorAsync();
        using HttpClient protecao = await ProtecaoAsync();
        await DecidirNaRevisaoAsync(isabella, diogo, protecao, cenario.PerfilId, migrada, propria);

        JsonElement trilhaDaMigrada = await LerAsync(diogo, RotaDaTrilha(migrada));
        JsonElement trilhaDaPropria = await LerAsync(diogo, RotaDaTrilha(propria));

        string revisaoId = (await RevisaoAsync(diogo, cenario.PerfilId)).GetProperty("id").GetGuid().ToString();
        ConferirTrilhas(trilhaDaMigrada, trilhaDaPropria, revisaoId);
    }

    [Fact(DisplayName = "A trilha da concessão não migrada pela revisão mostra a decisão e a revogação, ambas do sistema e com a referência à revisão")]
    public async Task Trilha_DaConcessaoNaoMigrada()
    {
        RevisaoAberta cenario = await AbrirRevisaoAsync(Leandro);
        Guid concessao = cenario.Concessoes[Leandro];
        using HttpClient diogo = await GestorAsync();
        Relogio.Avancar(DepoisDoPrazo);
        await ProcessarRevisoesAsync();

        JsonElement trilha = await LerAsync(diogo, RotaDaTrilha(concessao));

        string revisaoId = (await RevisaoAsync(diogo, cenario.PerfilId)).GetProperty("id").GetGuid().ToString();
        ConferirTrilhaDaNaoMigrada(trilha, revisaoId);
    }

    private static async Task DecidirNaRevisaoAsync(HttpClient isabella, HttpClient diogo, HttpClient protecao, Guid perfilId, Guid migrada, Guid propria)
    {
        using HttpResponseMessage envio = await EnviarDecisoesDaGestaoAsync(isabella, perfilId, [Decisao(migrada, "migrar")]);
        (await CorpoAsync(envio, HttpStatusCode.OK)).Should().NotBeNull();
        using HttpResponseMessage retida = await EnviarAsync(diogo, HttpMethod.Post, RotaDaRevisao(perfilId, $"/retidas/{propria}"), new { decisao = "migrar" });
        (await CorpoAsync(retida, HttpStatusCode.OK)).Should().NotBeNull();
        using HttpResponseMessage etapa = await EnviarAsync(protecao, HttpMethod.Post, RotaDaRevisao(perfilId, "/protecao-de-dados"),
            new { decisoes = new[] { Decisao(migrada, "migrar"), Decisao(propria, "migrar") } });
        SituacaoDe(await CorpoAsync(etapa, HttpStatusCode.OK), migrada).Should().Be("migrada");
    }

    private static void ConferirTrilhas(JsonElement trilhaDaMigrada, JsonElement trilhaDaPropria, string revisaoId)
    {
        JsonElement[] decisoes = EventosDoTipo(trilhaDaMigrada, "migracao_migrar");
        decisoes.Should().HaveCount(2);
        decisoes.Select(d => d.GetProperty("autor").GetProperty("contaId").GetString())
            .Should().BeEquivalentTo([Isabella.ContaId.ToString(), Marlene.ContaId.ToString()]);
        decisoes.Should().OnlyContain(d => d.GetProperty("ator").GetString() == "humano");
        decisoes.Should().OnlyContain(d => d.GetProperty("detalhe").GetString()!.Contains(revisaoId, StringComparison.Ordinal));
        JsonElement retencao = EventosDoTipo(trilhaDaPropria, "migracao_reter").Single();
        retencao.GetProperty("ator").GetString().Should().Be("sistema");
        DecisoresDe(trilhaDaPropria).Should().NotContain(Isabella.ContaId.ToString());
        DecisoresDe(trilhaDaPropria).Should().Contain([Diogo.ContaId.ToString(), Marlene.ContaId.ToString()]);
    }

    private static void ConferirTrilhaDaNaoMigrada(JsonElement trilha, string revisaoId)
    {
        JsonElement naoMigrada = EventosDoTipo(trilha, "migracao_nao_migrada").Single();
        naoMigrada.GetProperty("detalhe").GetString().Should().Contain(revisaoId);
        naoMigrada.GetProperty("ator").GetString().Should().Be("sistema");
        naoMigrada.TryGetProperty("autor", out JsonElement autorDaDecisao).Should().BeTrue();
        autorDaDecisao.ValueKind.Should().BeOneOf(JsonValueKind.Null, JsonValueKind.Undefined);
        JsonElement revogada = EventosDoTipo(trilha, "revogada_pela_migracao").Single();
        revogada.GetProperty("ator").GetString().Should().Be("sistema");
        DecisoresDe(trilha).Should().BeEmpty();
    }

    private static string RotaDaTrilha(Guid concessaoId) => $"/api/autorizacao/concessoes/{concessaoId}/trilha";

    private static JsonElement[] EventosDoTipo(JsonElement trilha, string tipo) =>
        [.. trilha.EnumerateArray().Where(e => e.GetProperty("tipo").GetString() == tipo)];

    /// <summary>Quem assina os eventos da migração; a concessão e a origem têm autores próprios, fora deste conjunto.</summary>
    private static string[] DecisoresDe(JsonElement trilha) =>
        [.. trilha.EnumerateArray()
            .Where(e => e.GetProperty("tipo").GetString()!.StartsWith("migracao_", StringComparison.Ordinal))
            .Where(e => e.TryGetProperty("autor", out JsonElement a) && a.ValueKind == JsonValueKind.Object)
            .Select(e => e.GetProperty("autor").GetProperty("contaId").GetString()!)];
}
