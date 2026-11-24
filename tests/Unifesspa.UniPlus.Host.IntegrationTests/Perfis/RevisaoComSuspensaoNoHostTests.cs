namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// A suspensão do perfil com a revisão das concessões aberta, pelo host real e com o relógio controlável: bloqueia as
/// concessões antigas e as migradas sem pausar o prazo, aceita o envio e não é desfeita nem reaberta por outra operação.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
public sealed class RevisaoComSuspensaoNoHostTests : RevisaoDasConcessoesNoHostTestBase
{
    private const string Laudos = "teste:laudos:consultar";

    private static readonly TimeSpan DepoisDoPrazo = TimeSpan.FromHours(73);

    public RevisaoComSuspensaoNoHostTests(MonolitoDaInstalacaoFixture fixture, HostDaRevisaoDasConcessoes hostDaClasse)
        : base(fixture, hostDaClasse)
    {
    }

    [Fact(DisplayName = "Suspender com a revisão aberta bloqueia as concessões que aguardam a migração, e o prazo continua correndo até o fim")]
    public async Task Suspender_BloqueiaAsAntigas_EOPrazoContinua()
    {
        RevisaoAberta cenario = await AbrirRevisaoAsync(Leandro, Betina);
        using HttpClient gestao = await GestaoAsync();
        DateTimeOffset prazo = (await RevisaoAsync(gestao, cenario.PerfilId)).GetProperty("terminaEm").GetDateTimeOffset();
        (await TemConcessaoAtivaAsync(Leandro, cenario.PerfilId)).Should().BeTrue();

        await SuspenderComSucessoAsync(cenario.PerfilId);

        (await TemConcessaoAtivaAsync(Leandro, cenario.PerfilId)).Should().BeFalse();
        (await TemConcessaoAtivaAsync(Betina, cenario.PerfilId)).Should().BeFalse();
        await ConferirRevisaoAbertaAsync(gestao, cenario.PerfilId, prazo);
        Relogio.Avancar(DepoisDoPrazo);
        (await ProcessarRevisoesAsync()).Should().BeGreaterThan(0);
        await ConferirRevisaoVencidaAsync(gestao, cenario);
    }

    [Fact(DisplayName = "O envio durante a suspensão é aceito e a concessão migrada segue bloqueada até a retomada completa")]
    public async Task EnvioDuranteASuspensao_MigradaSegueBloqueada()
    {
        RevisaoAberta cenario = await AbrirRevisaoAsync(Leandro, Betina);
        using HttpClient gestao = await GestaoAsync();
        using HttpClient protecao = await ProtecaoAsync();
        await SuspenderComSucessoAsync(cenario.PerfilId);

        JsonElement revisao = await MigrarTodasAsync(gestao, protecao, cenario);

        cenario.Concessoes.Values.Should().OnlyContain(id => SituacaoDe(revisao, id) == "MIGRADA");
        revisao.GetProperty("situacao").GetString().Should().Be("encerrada");
        (await TemConcessaoAtivaAsync(Leandro, cenario.PerfilId)).Should().BeFalse();
        (await TemConcessaoAtivaAsync(Betina, cenario.PerfilId)).Should().BeFalse();
        await ConferirRetomadaAsync(gestao, protecao, cenario.PerfilId);
    }

    [Fact(DisplayName = "Desativar e reativar o perfil depois do fim da revisão não a reabre")]
    public async Task ReativarOPerfil_NaoReabreARevisao()
    {
        RevisaoAberta cenario = await AbrirRevisaoAsync(Leandro);
        using HttpClient gestao = await GestaoAsync();
        using HttpClient protecao = await ProtecaoAsync();
        await MigrarTodasAsync(gestao, protecao, cenario);
        using HttpClient diogo = await GestorAsync();

        using HttpResponseMessage desativacao = await ChamadasDaContencao.DesativarAsync(diogo, cenario.PerfilId);
        using HttpResponseMessage reativacao = await ChamadasDaContencao.ReativarAsync(gestao, cenario.PerfilId);

        desativacao.StatusCode.Should().Be(HttpStatusCode.NoContent);
        reativacao.StatusCode.Should().Be(HttpStatusCode.NoContent);
        JsonElement revisao = await RevisaoAsync(gestao, cenario.PerfilId);
        revisao.GetProperty("situacao").GetString().Should().Be("encerrada");
        (await PerfilAsync(gestao, cenario.PerfilId)).GetProperty("ativo").GetBoolean().Should().BeTrue();
    }

    [Fact(DisplayName = "O envio com o perfil suspenso, numa concessão que já tem permissão protegida, calcula o mesmo acréscimo e as mesmas etapas que sem suspensão")]
    public async Task EnvioSuspenso_CalculaOMesmoAcrescimoQueSemSuspensao()
    {
        RevisaoAberta semSuspensao = await AbrirRevisaoComProtegidaAsync(Leandro);
        RevisaoAberta suspenso = await AbrirRevisaoComProtegidaAsync(Betina);
        await SuspenderComSucessoAsync(suspenso.PerfilId);
        using HttpClient gestao = await GestaoAsync();

        JsonElement itemSemSuspensao = await ItemAposOEnvioAsync(gestao, semSuspensao, Leandro);
        JsonElement itemSuspenso = await ItemAposOEnvioAsync(gestao, suspenso, Betina);

        itemSuspenso.GetProperty("acrescimo").EnumerateArray().Select(c => c.GetString()).Should().Equal(Laudos);
        itemSuspenso.GetProperty("acrescimo").GetRawText().Should().Be(itemSemSuspensao.GetProperty("acrescimo").GetRawText());
        itemSuspenso.GetProperty("exigeProtecaoDeDados").GetBoolean().Should().Be(itemSemSuspensao.GetProperty("exigeProtecaoDeDados").GetBoolean());
        itemSuspenso.GetProperty("situacao").GetString().Should().Be(itemSemSuspensao.GetProperty("situacao").GetString());
    }

    private static async Task ConferirRevisaoVencidaAsync(HttpClient gestao, RevisaoAberta cenario)
    {
        JsonElement vencida = await RevisaoAsync(gestao, cenario.PerfilId);
        vencida.GetProperty("situacao").GetString().Should().Be("encerrada");
        cenario.Concessoes.Values.Should().OnlyContain(id => SituacaoDe(vencida, id) == "NAO_MIGRADA");
    }

    private async Task ConferirRevisaoAbertaAsync(HttpClient gestao, Guid perfilId, DateTimeOffset prazo)
    {
        Relogio.Avancar(TimeSpan.FromHours(24));
        JsonElement correndo = await RevisaoAsync(gestao, perfilId);
        correndo.GetProperty("situacao").GetString().Should().Be("aberta");
        correndo.GetProperty("terminaEm").GetDateTimeOffset().Should().Be(prazo);
    }

    private async Task ConferirRetomadaAsync(HttpClient gestao, HttpClient protecao, Guid perfilId)
    {
        await RetomarComSucessoAsync(gestao, perfilId, "GESTAO_DE_ACESSOS");
        (await TemConcessaoAtivaAsync(Leandro, perfilId)).Should().BeFalse();
        await RetomarComSucessoAsync(protecao, perfilId, "PROTECAO_DE_DADOS");
        (await TemConcessaoAtivaAsync(Leandro, perfilId)).Should().BeTrue();
        (await TemConcessaoAtivaAsync(Betina, perfilId)).Should().BeTrue();
    }

    private async Task<RevisaoAberta> AbrirRevisaoComProtegidaAsync(Persona beneficiario)
    {
        using HttpClient gestor = await GestorAsync();
        Envio vigente = await PerfilVigenteAsync(gestor, Consultar, Sensivel);
        Guid concessao = await ConcessoesDeTeste.ConcederAsync(
            Fixture.Factory.Services,
            beneficiario.ContaId.ToString(),
            vigente.PerfilId,
            EscopoDaConcessao.DaUnidade(Guid.CreateVersion7()),
            email: beneficiario.Email);
        Envio nova = await CriarVersaoAsync(gestor, vigente.PerfilId, Nome(), Consultar, Sensivel, Laudos);
        await EnviarEVerificarAsync(gestor, nova);

        return new RevisaoAberta(vigente.PerfilId, nova.VersaoId, new Dictionary<Persona, Guid> { [beneficiario] = concessao });
    }

    private static async Task<JsonElement> ItemAposOEnvioAsync(HttpClient gestao, RevisaoAberta cenario, Persona beneficiario)
    {
        Guid concessao = cenario.Concessoes[beneficiario];
        using HttpResponseMessage envio = await EnviarDecisoesDaGestaoAsync(gestao, cenario.PerfilId, [Decisao(concessao, "MIGRAR")]);

        return ItemDe(await CorpoAsync(envio, HttpStatusCode.OK), concessao).Clone();
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

    private static async Task<JsonElement> MigrarTodasAsync(HttpClient gestao, HttpClient protecao, RevisaoAberta cenario)
    {
        object[] decisoes = [.. cenario.Concessoes.Values.Select(id => Decisao(id, "MIGRAR"))];
        using HttpResponseMessage envio = await EnviarDecisoesDaGestaoAsync(gestao, cenario.PerfilId, decisoes);
        JsonElement aposOEnvio = await CorpoAsync(envio, HttpStatusCode.OK);
        cenario.Concessoes.Values.Should().OnlyContain(id => SituacaoDe(aposOEnvio, id) == "AGUARDANDO_PROTECAO_DE_DADOS");
        using HttpResponseMessage etapa = await EnviarAsync(protecao, HttpMethod.Post, RotaDaRevisao(cenario.PerfilId, "/protecao-de-dados"), new { decisoes });

        return await CorpoAsync(etapa, HttpStatusCode.OK);
    }
}
