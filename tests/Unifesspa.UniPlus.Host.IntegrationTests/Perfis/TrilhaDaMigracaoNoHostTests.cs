namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;

/// <summary>
/// A trilha da concessão lida pela API depois da janela de migração: a decisão, o ator sistema na revogação pela
/// migração, a referência à janela e a autoria de quem decidiu.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class TrilhaDaMigracaoNoHostTests : JanelaDeMigracaoNoHostTestBase
{
    private static readonly TimeSpan DepoisDoPrazo = TimeSpan.FromHours(73);

    public TrilhaDaMigracaoNoHostTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "A trilha da concessão migrada mostra a decisão humana com a janela, e a da retida mostra a retenção do sistema sem o autor do lote")]
    public async Task Trilha_DaMigracaoAprovada()
    {
        JanelaAberta cenario = await AbrirJanelaAsync(Isabella, Leandro);
        Guid migrada = cenario.Concessoes[Leandro];
        Guid propria = cenario.Concessoes[Isabella];
        using HttpClient isabella = await GestaoAsync();
        using HttpClient diogo = await GestorAsync();
        using HttpClient protecao = await ProtecaoAsync();
        using HttpResponseMessage lote = await EnviarLoteAsync(isabella, cenario.PerfilId, [Decisao(migrada, "migrar")]);
        (await CorpoAsync(lote, HttpStatusCode.OK)).Should().NotBeNull();
        using HttpResponseMessage retida = await EnviarAsync(
            diogo, HttpMethod.Post, RotaDaJanela(cenario.PerfilId, $"/retidas/{propria}"), new { decisao = "migrar" });
        (await CorpoAsync(retida, HttpStatusCode.OK)).Should().NotBeNull();
        using HttpResponseMessage etapa = await EnviarAsync(
            protecao,
            HttpMethod.Post,
            RotaDaJanela(cenario.PerfilId, "/protecao-de-dados"),
            new { decisoes = new[] { Decisao(migrada, "migrar"), Decisao(propria, "migrar") } });
        SituacaoDe(await CorpoAsync(etapa, HttpStatusCode.OK), migrada).Should().Be("migrada");

        JsonElement trilhaDaMigrada = await LerAsync(diogo, RotaDaTrilha(migrada));
        JsonElement trilhaDaPropria = await LerAsync(diogo, RotaDaTrilha(propria));

        string janelaId = (await JanelaAsync(diogo, cenario.PerfilId)).GetProperty("id").GetGuid().ToString();
        JsonElement[] decisoes = EventosDoTipo(trilhaDaMigrada, "migracao_migrar");
        decisoes.Should().HaveCount(2);
        decisoes.Select(d => d.GetProperty("autor").GetProperty("sub").GetString())
            .Should().BeEquivalentTo([Isabella.Sub.ToString(), Marlene.Sub.ToString()]);
        decisoes.Should().OnlyContain(d => d.GetProperty("ator").GetString() == "humano");
        decisoes.Should().OnlyContain(d => d.GetProperty("detalhe").GetString()!.Contains(janelaId, StringComparison.Ordinal));
        JsonElement retencao = EventosDoTipo(trilhaDaPropria, "migracao_reter").Single();
        retencao.GetProperty("ator").GetString().Should().Be("sistema");
        DecisoresDe(trilhaDaPropria).Should().NotContain(Isabella.Sub.ToString());
        DecisoresDe(trilhaDaPropria).Should().Contain([Diogo.Sub.ToString(), Marlene.Sub.ToString()]);
    }

    [Fact(DisplayName = "A trilha da concessão não migrada pela janela mostra a decisão e a revogação, ambas do sistema e com a referência à janela")]
    public async Task Trilha_DaConcessaoNaoMigrada()
    {
        JanelaAberta cenario = await AbrirJanelaAsync(Leandro);
        Guid concessao = cenario.Concessoes[Leandro];
        using HttpClient diogo = await GestorAsync();
        Relogio.Avancar(DepoisDoPrazo);
        await ProcessarJanelasAsync();

        JsonElement trilha = await LerAsync(diogo, RotaDaTrilha(concessao));

        JsonElement naoMigrada = EventosDoTipo(trilha, "migracao_nao_migrada").Single();
        string janelaId = (await JanelaAsync(diogo, cenario.PerfilId)).GetProperty("id").GetGuid().ToString();
        naoMigrada.GetProperty("detalhe").GetString().Should().Contain(janelaId);
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
            .Select(e => e.GetProperty("autor").GetProperty("sub").GetString()!)];
}
