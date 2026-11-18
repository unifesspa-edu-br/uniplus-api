namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Net;

using AwesomeAssertions;

using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;

/// <summary>
/// A suspensão e o envio da revisão das concessões disputam a trava do perfil: o teste a segura, enfileira as duas chamadas
/// nas duas ordens e confere que uma espera a outra, sem ciclo, e que o estado final é o mesmo.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
public sealed class ConcorrenciaDaSuspensaoComARevisaoNoHostTests : RevisaoDasConcessoesNoHostTestBase
{
    public ConcorrenciaDaSuspensaoComARevisaoNoHostTests(MonolitoDaInstalacaoFixture fixture, HostDaRevisaoDasConcessoes hostDaClasse)
        : base(fixture, hostDaClasse)
    {
    }

    [Fact(DisplayName = "A suspensão que chega antes do envio o deixa esperando, e o envio é aceito com a concessão bloqueada")]
    public async Task SuspensaoAntesDoEnvio_EnvioEhAceito()
    {
        RevisaoAberta cenario = await AbrirRevisaoAsync(Leandro);
        using HttpClient diogo = await GestorAsync();
        using HttpClient gestao = await GestaoAsync();

        HttpResponseMessage[] respostas = await EmFilaSobATravaAsync(
            cenario.PerfilId,
            () => ChamadasDaContencao.SuspenderAsync(diogo, cenario.PerfilId),
            () => EnviarDecisoesDaGestaoAsync(gestao, cenario.PerfilId, [Decisao(cenario.Concessoes[Leandro], "migrar")]));

        await ConferirAsync(respostas, cenario);
    }

    [Fact(DisplayName = "O envio que chega antes da suspensão a deixa esperando, e a suspensão bloqueia a concessão em migração")]
    public async Task EnvioAntesDaSuspensao_SuspensaoBloqueia()
    {
        RevisaoAberta cenario = await AbrirRevisaoAsync(Leandro);
        using HttpClient diogo = await GestorAsync();
        using HttpClient gestao = await GestaoAsync();

        HttpResponseMessage[] respostas = await EmFilaSobATravaAsync(
            cenario.PerfilId,
            () => EnviarDecisoesDaGestaoAsync(gestao, cenario.PerfilId, [Decisao(cenario.Concessoes[Leandro], "migrar")]),
            () => ChamadasDaContencao.SuspenderAsync(diogo, cenario.PerfilId));

        await ConferirAsync([respostas[1], respostas[0]], cenario);
    }

    private async Task ConferirAsync(HttpResponseMessage[] suspensaoEEnvio, RevisaoAberta cenario)
    {
        try
        {
            suspensaoEEnvio[0].StatusCode.Should().Be(HttpStatusCode.NoContent);
            suspensaoEEnvio[1].StatusCode.Should().Be(HttpStatusCode.OK, await suspensaoEEnvio[1].Content.ReadAsStringAsync());
            using HttpClient gestao = await GestaoAsync();
            (await RevisaoAsync(gestao, cenario.PerfilId)).GetProperty("decisoesEnviadas").GetBoolean().Should().BeTrue();
            (await TemConcessaoAtivaAsync(Leandro, cenario.PerfilId)).Should().BeFalse();
        }
        finally
        {
            DescartarRespostas(suspensaoEEnvio);
        }
    }
    private static void DescartarRespostas(HttpResponseMessage[] respostas)
    {
        foreach (HttpResponseMessage resposta in respostas)
        {
            resposta.Dispose();
        }
    }

}
