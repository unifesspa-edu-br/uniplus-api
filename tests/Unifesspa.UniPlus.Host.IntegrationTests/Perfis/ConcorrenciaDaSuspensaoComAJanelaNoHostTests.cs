namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Diagnostics.CodeAnalysis;
using System.Net;

using AwesomeAssertions;

using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;

/// <summary>
/// A suspensão e o lote da janela de migração disputam a trava do perfil: o teste a segura, enfileira as duas chamadas
/// nas duas ordens e confere que uma espera a outra, sem ciclo, e que o estado final é o mesmo.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class ConcorrenciaDaSuspensaoComAJanelaNoHostTests : JanelaDeMigracaoNoHostTestBase
{
    public ConcorrenciaDaSuspensaoComAJanelaNoHostTests(MonolitoDaInstalacaoFixture fixture, HostDaJanelaDeMigracao hostDaClasse)
        : base(fixture, hostDaClasse)
    {
    }

    [Fact(DisplayName = "A suspensão que chega antes do lote o deixa esperando, e o lote é aceito com a concessão bloqueada")]
    public async Task SuspensaoAntesDoLote_LoteEhAceito()
    {
        JanelaAberta cenario = await AbrirJanelaAsync(Leandro);
        using HttpClient diogo = await GestorAsync();
        using HttpClient gestao = await GestaoAsync();

        HttpResponseMessage[] respostas = await EmFilaSobATravaAsync(
            cenario.PerfilId,
            () => ChamadasDaContencao.SuspenderAsync(diogo, cenario.PerfilId),
            () => EnviarLoteAsync(gestao, cenario.PerfilId, [Decisao(cenario.Concessoes[Leandro], "migrar")]));

        await ConferirAsync(respostas, cenario);
    }

    [Fact(DisplayName = "O lote que chega antes da suspensão a deixa esperando, e a suspensão bloqueia a concessão em migração")]
    public async Task LoteAntesDaSuspensao_SuspensaoBloqueia()
    {
        JanelaAberta cenario = await AbrirJanelaAsync(Leandro);
        using HttpClient diogo = await GestorAsync();
        using HttpClient gestao = await GestaoAsync();

        HttpResponseMessage[] respostas = await EmFilaSobATravaAsync(
            cenario.PerfilId,
            () => EnviarLoteAsync(gestao, cenario.PerfilId, [Decisao(cenario.Concessoes[Leandro], "migrar")]),
            () => ChamadasDaContencao.SuspenderAsync(diogo, cenario.PerfilId));

        await ConferirAsync([respostas[1], respostas[0]], cenario);
    }

    private async Task ConferirAsync(HttpResponseMessage[] suspensaoELote, JanelaAberta cenario)
    {
        try
        {
            suspensaoELote[0].StatusCode.Should().Be(HttpStatusCode.NoContent);
            suspensaoELote[1].StatusCode.Should().Be(HttpStatusCode.OK, await suspensaoELote[1].Content.ReadAsStringAsync());
            using HttpClient gestao = await GestaoAsync();
            (await JanelaAsync(gestao, cenario.PerfilId)).GetProperty("loteEnviado").GetBoolean().Should().BeTrue();
            (await TemConcessaoAtivaAsync(Leandro, cenario.PerfilId)).Should().BeFalse();
        }
        finally
        {
            foreach (HttpResponseMessage resposta in suspensaoELote)
            {
                resposta.Dispose();
            }
        }
    }
}
