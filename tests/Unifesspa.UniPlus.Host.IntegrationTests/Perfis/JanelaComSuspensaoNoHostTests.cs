namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;

/// <summary>
/// A suspensão do perfil com a janela de migração aberta, pelo host real e com o relógio controlável: bloqueia as
/// concessões antigas e as migradas sem pausar o prazo, aceita o lote e não é desfeita nem reaberta por outra operação.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class JanelaComSuspensaoNoHostTests : JanelaDeMigracaoNoHostTestBase
{
    private static readonly TimeSpan DepoisDoPrazo = TimeSpan.FromHours(73);

    public JanelaComSuspensaoNoHostTests(MonolitoDaInstalacaoFixture fixture, HostDaJanelaDeMigracao hostDaClasse)
        : base(fixture, hostDaClasse)
    {
    }

    [Fact(DisplayName = "Suspender com a janela aberta bloqueia as concessões que aguardam a migração, e o prazo continua correndo até o fim")]
    public async Task Suspender_BloqueiaAsAntigas_EOPrazoContinua()
    {
        JanelaAberta cenario = await AbrirJanelaAsync(Leandro, Betina);
        using HttpClient gestao = await GestaoAsync();
        DateTimeOffset prazo = (await JanelaAsync(gestao, cenario.PerfilId)).GetProperty("prazo").GetDateTimeOffset();
        (await TemConcessaoAtivaAsync(Leandro, cenario.PerfilId)).Should().BeTrue();

        await SuspenderComSucessoAsync(cenario.PerfilId);

        (await TemConcessaoAtivaAsync(Leandro, cenario.PerfilId)).Should().BeFalse();
        (await TemConcessaoAtivaAsync(Betina, cenario.PerfilId)).Should().BeFalse();
        Relogio.Avancar(TimeSpan.FromHours(24));
        JsonElement correndo = await JanelaAsync(gestao, cenario.PerfilId);
        correndo.GetProperty("situacao").GetString().Should().Be("aberta");
        correndo.GetProperty("prazo").GetDateTimeOffset().Should().Be(prazo);
        Relogio.Avancar(DepoisDoPrazo);
        (await ProcessarJanelasAsync()).Should().BeGreaterThan(0);
        JsonElement vencida = await JanelaAsync(gestao, cenario.PerfilId);
        vencida.GetProperty("situacao").GetString().Should().Be("encerrada");
        cenario.Concessoes.Values.Should().OnlyContain(id => SituacaoDe(vencida, id) == "nao_migrada");
    }

    [Fact(DisplayName = "O lote durante a suspensão é aceito e a concessão migrada segue bloqueada até a retomada completa")]
    public async Task LoteDuranteASuspensao_MigradaSegueBloqueada()
    {
        JanelaAberta cenario = await AbrirJanelaAsync(Leandro, Betina);
        using HttpClient gestao = await GestaoAsync();
        using HttpClient protecao = await ProtecaoAsync();
        await SuspenderComSucessoAsync(cenario.PerfilId);

        JsonElement janela = await MigrarTodasAsync(gestao, protecao, cenario);

        cenario.Concessoes.Values.Should().OnlyContain(id => SituacaoDe(janela, id) == "migrada");
        janela.GetProperty("situacao").GetString().Should().Be("encerrada");
        (await TemConcessaoAtivaAsync(Leandro, cenario.PerfilId)).Should().BeFalse();
        (await TemConcessaoAtivaAsync(Betina, cenario.PerfilId)).Should().BeFalse();
        await RetomarComSucessoAsync(gestao, cenario.PerfilId, "gestao_de_acessos");
        (await TemConcessaoAtivaAsync(Leandro, cenario.PerfilId)).Should().BeFalse();
        await RetomarComSucessoAsync(protecao, cenario.PerfilId, "protecao_de_dados");
        (await TemConcessaoAtivaAsync(Leandro, cenario.PerfilId)).Should().BeTrue();
        (await TemConcessaoAtivaAsync(Betina, cenario.PerfilId)).Should().BeTrue();
    }

    [Fact(DisplayName = "Desativar e reativar o perfil depois do fim da janela não a reabre")]
    public async Task ReativarOPerfil_NaoReabreAJanela()
    {
        JanelaAberta cenario = await AbrirJanelaAsync(Leandro);
        using HttpClient gestao = await GestaoAsync();
        using HttpClient protecao = await ProtecaoAsync();
        await MigrarTodasAsync(gestao, protecao, cenario);
        using HttpClient diogo = await GestorAsync();

        using HttpResponseMessage desativacao = await ChamadasDaContencao.DesativarAsync(diogo, cenario.PerfilId);
        using HttpResponseMessage reativacao = await ChamadasDaContencao.ReativarAsync(gestao, cenario.PerfilId);

        desativacao.StatusCode.Should().Be(HttpStatusCode.NoContent);
        reativacao.StatusCode.Should().Be(HttpStatusCode.NoContent);
        JsonElement janela = await JanelaAsync(gestao, cenario.PerfilId);
        janela.GetProperty("situacao").GetString().Should().Be("encerrada");
        (await PerfilAsync(gestao, cenario.PerfilId)).GetProperty("ativo").GetBoolean().Should().BeTrue();
    }

    private async Task SuspenderComSucessoAsync(Guid perfilId)
    {
        using HttpClient diogo = await GestorAsync();
        using HttpResponseMessage resposta = await ChamadasDaContencao.SuspenderAsync(diogo, perfilId);
        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent, await resposta.Content.ReadAsStringAsync());
    }

    private static async Task RetomarComSucessoAsync(HttpClient cliente, Guid perfilId, string etapa)
    {
        using HttpResponseMessage resposta = await ChamadasDaContencao.RetomarAsync(cliente, perfilId, etapa);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
    }

    private static async Task<JsonElement> MigrarTodasAsync(HttpClient gestao, HttpClient protecao, JanelaAberta cenario)
    {
        object[] decisoes = [.. cenario.Concessoes.Values.Select(id => Decisao(id, "migrar"))];
        using HttpResponseMessage lote = await EnviarLoteAsync(gestao, cenario.PerfilId, decisoes);
        JsonElement aposOLote = await CorpoAsync(lote, HttpStatusCode.OK);
        cenario.Concessoes.Values.Should().OnlyContain(id => SituacaoDe(aposOLote, id) == "aguardando_protecao_de_dados");
        using HttpResponseMessage etapa = await EnviarAsync(protecao, HttpMethod.Post, RotaDaJanela(cenario.PerfilId, "/protecao-de-dados"), new { decisoes });

        return await CorpoAsync(etapa, HttpStatusCode.OK);
    }
}
