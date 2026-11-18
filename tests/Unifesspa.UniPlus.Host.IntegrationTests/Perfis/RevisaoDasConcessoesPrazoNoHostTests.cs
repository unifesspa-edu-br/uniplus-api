namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// O prazo da revisão das concessões com o relógio controlável: a decisão de acesso o confere sem a rotina, a rotina
/// materializa o fim e emite os lembretes, e a operação que chega depois do prazo encontra a revisão encerrada.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
public sealed class RevisaoDasConcessoesPrazoNoHostTests : RevisaoDasConcessoesNoHostTestBase
{
    private static readonly TimeSpan DepoisDoPrazo = TimeSpan.FromHours(73);

    public RevisaoDasConcessoesPrazoNoHostTests(MonolitoDaInstalacaoFixture fixture, HostDaRevisaoDasConcessoes hostDaClasse)
        : base(fixture, hostDaClasse)
    {
    }

    [Fact(DisplayName = "Depois do prazo e com a rotina parada, a concessão antiga já não dá acesso")]
    public async Task DepoisDoPrazo_SemARotina_AConcessaoNaoDaAcesso()
    {
        RevisaoAberta cenario = await AbrirRevisaoAsync(Leandro);
        (await TemConcessaoAtivaAsync(Leandro, cenario.PerfilId)).Should().BeTrue();

        Relogio.Avancar(DepoisDoPrazo);

        (await TemConcessaoAtivaAsync(Leandro, cenario.PerfilId)).Should().BeFalse();
    }

    [Fact(DisplayName = "O envio que espera o bloqueio de uma concessão e atravessa o prazo encontra a revisão encerrada e não migra")]
    public async Task Envio_AtravessaOPrazoNaEsperaDaConcessao_EhRecusado()
    {
        RevisaoAberta cenario = await AbrirRevisaoAsync(Leandro);
        Guid concessao = cenario.Concessoes[Leandro];
        using HttpClient gestao = await GestaoAsync();
        await using TransacaoRetidaDeTeste bloqueio = await TransacaoRetidaDeTeste.AbrirAsync(Fixture.Factory.Services);
        await bloqueio.Servicos.GetRequiredService<UniPlusDbContext>().Database.ExecuteSqlRawAsync(
            "SELECT 1 FROM autorizacao.concessoes WHERE id = {0} FOR UPDATE", concessao);

        using HttpResponseMessage resposta = await EnviarAposOPrazoAsync(gestao, cenario.PerfilId, concessao, bloqueio);

        await ConferirRecusaDaMigracaoAsync(resposta, HttpStatusCode.UnprocessableEntity, "prazo_encerrado");
        SituacaoDe(await RevisaoAsync(gestao, cenario.PerfilId), concessao).Should().Be("nao_migrada");
    }

    [Fact(DisplayName = "A rotina depois do prazo encerra a revisão e marca quem não migrou, uma só vez")]
    public async Task Rotina_DepoisDoPrazo_EncerraARevisao()
    {
        RevisaoAberta cenario = await AbrirRevisaoAsync(Leandro);
        using HttpClient gestao = await GestaoAsync();
        Relogio.Avancar(DepoisDoPrazo);

        int primeira = await ProcessarRevisoesAsync();
        int segunda = await ProcessarRevisoesAsync();

        primeira.Should().BeGreaterThanOrEqualTo(1);
        segunda.Should().Be(0);
        JsonElement revisao = await RevisaoAsync(gestao, cenario.PerfilId);
        revisao.GetProperty("situacao").GetString().Should().Be("encerrada");
        revisao.GetProperty("forma").GetString().Should().Be("prazo");
        SituacaoDe(revisao, cenario.Concessoes[Leandro]).Should().Be("nao_migrada");
    }

    [Fact(DisplayName = "A leitura da revisão encerrada lista só as concessões que ela registrou, não as de uma versão posterior")]
    public async Task Leitura_DaRevisaoEncerrada_NaoListaConcessaoDeVersaoPosterior()
    {
        RevisaoAberta cenario = await AbrirRevisaoAsync(Leandro);
        using HttpClient gestao = await GestaoAsync();
        Relogio.Avancar(DepoisDoPrazo);
        await ProcessarRevisoesAsync();
        await TrocarComposicaoAsync(cenario.PerfilId, Consultar);
        Guid posterior = await ConcederABetinaAsync(cenario.PerfilId);

        JsonElement revisao = await RevisaoAsync(gestao, cenario.PerfilId);

        revisao.GetProperty("situacao").GetString().Should().Be("encerrada");
        revisao.GetProperty("itens").EnumerateArray().Select(i => i.GetProperty("concessaoId").GetGuid())
            .Should().BeEquivalentTo([cenario.Concessoes[Leandro]]).And.NotContain(posterior);
    }

    [Fact(DisplayName = "O envio que chega depois do prazo grava o encerramento e é recusado, com a mesma resposta na repetição")]
    public async Task Envio_DepoisDoPrazo_GravaORecusa()
    {
        RevisaoAberta cenario = await AbrirRevisaoAsync(Leandro);
        using HttpClient gestao = await GestaoAsync();
        string chave = Guid.NewGuid().ToString();
        Relogio.Avancar(DepoisDoPrazo);

        using HttpResponseMessage primeira = await EnviarDecisoesDaGestaoAsync(gestao, cenario.PerfilId, [Decisao(cenario.Concessoes[Leandro], "migrar")], chave: chave);
        using HttpResponseMessage repeticao = await EnviarDecisoesDaGestaoAsync(gestao, cenario.PerfilId, [Decisao(cenario.Concessoes[Leandro], "migrar")], chave: chave);

        await ConferirRecusaDaMigracaoAsync(primeira, HttpStatusCode.UnprocessableEntity, "prazo_encerrado");
        await ConferirRecusaDaMigracaoAsync(repeticao, HttpStatusCode.UnprocessableEntity, "prazo_encerrado");
        JsonElement revisao = await RevisaoAsync(gestao, cenario.PerfilId);
        revisao.GetProperty("situacao").GetString().Should().Be("encerrada");
        SituacaoDe(revisao, cenario.Concessoes[Leandro]).Should().Be("nao_migrada");
    }

    [Fact(DisplayName = "A rotina emite o lembrete de 48 horas e depois o de 24 horas, cada um uma só vez")]
    public async Task Rotina_EmiteOsLembretes()
    {
        RevisaoAberta cenario = await AbrirRevisaoAsync(Leandro);
        (await AvisoGravadoEmAsync(cenario.PerfilId, true)).Should().BeNull();

        Relogio.Avancar(TimeSpan.FromHours(25));
        await ProcessarRevisoesAsync();
        DateTimeOffset? aviso48 = await AvisoGravadoEmAsync(cenario.PerfilId, true);
        await ProcessarRevisoesAsync();

        aviso48.Should().NotBeNull();
        await ConferirAvisoDe24HorasAsync(cenario.PerfilId, aviso48);
    }

    [Fact(DisplayName = "Dois envios simultâneos: um grava e o outro encontra o envio já enviado")]
    public async Task DoisEnviosSimultaneos_UmSoGrava()
    {
        RevisaoAberta cenario = await AbrirRevisaoAsync(Leandro);
        using HttpClient gestao = await GestaoAsync();
        object[] decisoes = [Decisao(cenario.Concessoes[Leandro], "migrar")];

        HttpResponseMessage[] respostas = await EmFilaSobOBloqueioAsync(
            cenario.PerfilId,
            () => EnviarDecisoesDaGestaoAsync(gestao, cenario.PerfilId, decisoes),
            () => EnviarDecisoesDaGestaoAsync(gestao, cenario.PerfilId, decisoes));

        respostas.Select(r => r.StatusCode).Should().BeEquivalentTo([HttpStatusCode.OK, HttpStatusCode.Conflict]);
        await ConferirRecusaDaMigracaoAsync(respostas[1], HttpStatusCode.Conflict, "decisoes_ja_enviadas");
        DescartarRespostas(respostas);
    }
    private async Task<HttpResponseMessage> EnviarAposOPrazoAsync(HttpClient gestao, Guid perfilId, Guid concessao, TransacaoRetidaDeTeste bloqueio)
    {
        Task<HttpResponseMessage> envio = EnviarDecisoesDaGestaoAsync(gestao, perfilId, [Decisao(concessao, "migrar")]);
        await bloqueio.AguardarBloqueioAsync();
        Relogio.Avancar(DepoisDoPrazo);
        await bloqueio.ConfirmarAsync();
        return await envio;
    }

    private Task<Guid> ConcederABetinaAsync(Guid perfilId) => ConcessoesDeTeste.ConcederAsync(
        Fixture.Factory.Services, Betina.ContaId.ToString(), perfilId,
        EscopoDaConcessao.DaUnidade(Guid.CreateVersion7()), email: Betina.Email);

    private async Task ConferirAvisoDe24HorasAsync(Guid perfilId, DateTimeOffset? aviso48)
    {
        (await AvisoGravadoEmAsync(perfilId, true)).Should().Be(aviso48);
        (await AvisoGravadoEmAsync(perfilId, false)).Should().BeNull();
        Relogio.Avancar(TimeSpan.FromHours(24));
        await ProcessarRevisoesAsync();
        (await AvisoGravadoEmAsync(perfilId, false)).Should().NotBeNull();
    }

    private static void DescartarRespostas(HttpResponseMessage[] respostas)
    {
        foreach (HttpResponseMessage resposta in respostas)
        {
            resposta.Dispose();
        }
    }

}
