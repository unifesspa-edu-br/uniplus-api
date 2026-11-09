namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;

using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;

/// <summary>
/// O prazo da janela de migração com o relógio controlável: a decisão de acesso o confere sem a rotina, a rotina
/// materializa o fim e emite os lembretes, e a operação que chega depois do prazo encontra a janela encerrada.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class JanelaDeMigracaoPrazoNoHostTests : JanelaDeMigracaoNoHostTestBase
{
    private static readonly TimeSpan DepoisDoPrazo = TimeSpan.FromHours(73);

    public JanelaDeMigracaoPrazoNoHostTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "Depois do prazo e com a rotina parada, a concessão antiga já não dá acesso")]
    public async Task DepoisDoPrazo_SemARotina_AConcessaoNaoDaAcesso()
    {
        JanelaAberta cenario = await AbrirJanelaAsync(Leandro);
        (await TemConcessaoAtivaAsync(Leandro, cenario.PerfilId)).Should().BeTrue();

        Relogio.Avancar(DepoisDoPrazo);

        (await TemConcessaoAtivaAsync(Leandro, cenario.PerfilId)).Should().BeFalse();
    }

    [Fact(DisplayName = "O lote que espera a trava de uma concessão e atravessa o prazo encontra a janela encerrada e não migra")]
    public async Task Lote_AtravessaOPrazoNaEsperaDaConcessao_EhRecusado()
    {
        JanelaAberta cenario = await AbrirJanelaAsync(Leandro);
        Guid concessao = cenario.Concessoes[Leandro];
        using HttpClient gestao = await GestaoAsync();
        await using TransacaoRetidaDeTeste trava = await TransacaoRetidaDeTeste.AbrirAsync(Fixture.Factory.Services);
        await trava.Servicos.GetRequiredService<AutorizacaoDbContext>().Database.ExecuteSqlRawAsync(
            "SELECT 1 FROM autorizacao.concessoes WHERE id = {0} FOR UPDATE", concessao);

        Task<HttpResponseMessage> lote = EnviarLoteAsync(gestao, cenario.PerfilId, [Decisao(concessao, "migrar")]);
        await TransacaoRetidaDeTeste.AguardarBloqueioAsync(Fixture.Factory.Services);
        Relogio.Avancar(DepoisDoPrazo);
        await trava.ConfirmarAsync();
        using HttpResponseMessage resposta = await lote;

        await ConferirRecusaDaMigracaoAsync(resposta, HttpStatusCode.UnprocessableEntity, "prazo_encerrado");
        SituacaoDe(await JanelaAsync(gestao, cenario.PerfilId), concessao).Should().Be("nao_migrada");
    }

    [Fact(DisplayName = "A rotina depois do prazo encerra a janela e marca quem não migrou, uma só vez")]
    public async Task Rotina_DepoisDoPrazo_EncerraAJanela()
    {
        JanelaAberta cenario = await AbrirJanelaAsync(Leandro);
        using HttpClient gestao = await GestaoAsync();
        Relogio.Avancar(DepoisDoPrazo);

        int primeira = await ProcessarJanelasAsync();
        int segunda = await ProcessarJanelasAsync();

        primeira.Should().BeGreaterThanOrEqualTo(1);
        segunda.Should().Be(0);
        JsonElement janela = await JanelaAsync(gestao, cenario.PerfilId);
        janela.GetProperty("situacao").GetString().Should().Be("encerrada");
        janela.GetProperty("forma").GetString().Should().Be("prazo");
        SituacaoDe(janela, cenario.Concessoes[Leandro]).Should().Be("nao_migrada");
    }

    [Fact(DisplayName = "O lote que chega depois do prazo grava o encerramento e é recusado, com a mesma resposta no replay")]
    public async Task Lote_DepoisDoPrazo_GravaORecusa()
    {
        JanelaAberta cenario = await AbrirJanelaAsync(Leandro);
        using HttpClient gestao = await GestaoAsync();
        string chave = Guid.NewGuid().ToString();
        Relogio.Avancar(DepoisDoPrazo);

        using HttpResponseMessage primeira = await EnviarLoteAsync(gestao, cenario.PerfilId, [Decisao(cenario.Concessoes[Leandro], "migrar")], chave: chave);
        using HttpResponseMessage replay = await EnviarLoteAsync(gestao, cenario.PerfilId, [Decisao(cenario.Concessoes[Leandro], "migrar")], chave: chave);

        await ConferirRecusaDaMigracaoAsync(primeira, HttpStatusCode.UnprocessableEntity, "prazo_encerrado");
        await ConferirRecusaDaMigracaoAsync(replay, HttpStatusCode.UnprocessableEntity, "prazo_encerrado");
        JsonElement janela = await JanelaAsync(gestao, cenario.PerfilId);
        janela.GetProperty("situacao").GetString().Should().Be("encerrada");
        SituacaoDe(janela, cenario.Concessoes[Leandro]).Should().Be("nao_migrada");
    }

    [Fact(DisplayName = "A rotina emite o lembrete de 48 horas e depois o de 24 horas, cada um uma só vez")]
    public async Task Rotina_EmiteOsLembretes()
    {
        JanelaAberta cenario = await AbrirJanelaAsync(Leandro);
        (await AvisoGravadoEmAsync(cenario.PerfilId, true)).Should().BeNull();

        Relogio.Avancar(TimeSpan.FromHours(25));
        await ProcessarJanelasAsync();
        DateTimeOffset? aviso48 = await AvisoGravadoEmAsync(cenario.PerfilId, true);
        await ProcessarJanelasAsync();

        aviso48.Should().NotBeNull();
        (await AvisoGravadoEmAsync(cenario.PerfilId, true)).Should().Be(aviso48);
        (await AvisoGravadoEmAsync(cenario.PerfilId, false)).Should().BeNull();

        Relogio.Avancar(TimeSpan.FromHours(24));
        await ProcessarJanelasAsync();

        (await AvisoGravadoEmAsync(cenario.PerfilId, false)).Should().NotBeNull();
    }

    [Fact(DisplayName = "Dois lotes simultâneos: um grava e o outro encontra o lote já enviado")]
    public async Task DoisLotesSimultaneos_UmSoGrava()
    {
        JanelaAberta cenario = await AbrirJanelaAsync(Leandro);
        using HttpClient gestao = await GestaoAsync();
        object[] decisoes = [Decisao(cenario.Concessoes[Leandro], "migrar")];

        HttpResponseMessage[] respostas = await EmFilaSobATravaAsync(
            cenario.PerfilId,
            () => EnviarLoteAsync(gestao, cenario.PerfilId, decisoes),
            () => EnviarLoteAsync(gestao, cenario.PerfilId, decisoes));

        respostas.Select(r => r.StatusCode).Should().BeEquivalentTo([HttpStatusCode.OK, HttpStatusCode.Conflict]);
        await ConferirRecusaDaMigracaoAsync(respostas[1], HttpStatusCode.Conflict, "lote_ja_enviado");
        foreach (HttpResponseMessage resposta in respostas)
        {
            resposta.Dispose();
        }
    }
}
