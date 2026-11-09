namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Diagnostics.CodeAnalysis;
using System.Net;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;

/// <summary>
/// O instante de uma transição sobre o perfil é lido depois da trava: a requisição que espera a trava e atravessa o
/// prazo encontra a pendente vencida.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class PrazoDaVersaoSobATravaNoHostTests : AprovacaoDaVersaoNoHostTestBase
{
    private static readonly TimeSpan MargemDoPrazo = TimeSpan.FromSeconds(3);

    public PrazoDaVersaoSobATravaNoHostTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "A decisão que espera a trava e atravessa o prazo encontra a versão expirada e não a põe em vigor")]
    public async Task Decisao_AtravessaOPrazoNaEspera_EncontraAVersaoExpirada()
    {
        (Envio vigente, Envio versao) = await VigenteEPendenteVerificadaAsync();
        using HttpClient gestao = await GestaoAsync();
        await ExpirarEmAsync(versao, MargemDoPrazo);

        using HttpResponseMessage resposta = await ChamarAtravessandoOPrazoAsync(vigente.PerfilId, () => AprovarBrutoAsync(gestao, versao, EtapaGestao));

        await ConferirRecusaDoPerfilAsync(resposta, HttpStatusCode.UnprocessableEntity, "versao_expirada");
        (await SituacaoDaVersaoNoBancoAsync(versao.VersaoId)).Should().Be("expirada");
        (await DecisoesAsync(versao.VersaoId)).Should().BeEmpty();
        (await VigenciasAsync(vigente.PerfilId)).Should().ContainSingle(v => v.Aberta && v.VersaoId == vigente.VersaoId);
    }

    [Fact(DisplayName = "A remoção que espera a trava e atravessa o prazo expira a pendente em vez de cancelá-la por contenção")]
    public async Task Remocao_AtravessaOPrazoNaEspera_ExpiraAPendente()
    {
        (Envio vigente, Envio versao) = await VigenteEPendenteVerificadaAsync();
        using HttpClient gestor = await GestorAsync();
        await ExpirarEmAsync(versao, MargemDoPrazo);

        using HttpResponseMessage resposta = await ChamarAtravessandoOPrazoAsync(
            vigente.PerfilId, () => RemoverBrutoAsync(gestor, vigente.PerfilId, [Sensivel]));

        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
        (await SituacaoDaVersaoNoBancoAsync(versao.VersaoId)).Should().Be("expirada");
    }

    private async Task<(Envio Vigente, Envio Versao)> VigenteEPendenteVerificadaAsync()
    {
        await ResetarAsync();
        using HttpClient gestor = await GestorAsync();
        Envio vigente = await PerfilVigenteAsync(gestor, Consultar, Sensivel);

        return (vigente, await PendenteVerificadaAsync(gestor, vigente.PerfilId, Consultar, Sensivel));
    }

    private Task ExpirarEmAsync(Envio versao, TimeSpan prazo) =>
        ExecutarSqlAsync(
            "UPDATE autorizacao.versoes_do_perfil SET expira_em = now() + {0} WHERE id = {1}", prazo, versao.VersaoId);

    private async Task<HttpResponseMessage> ChamarAtravessandoOPrazoAsync(Guid perfilId, Func<Task<HttpResponseMessage>> chamada)
    {
        await using TransacaoRetidaDeTeste trava = await TravarOPerfilAsync(perfilId);
        Task<HttpResponseMessage> disparada = chamada();
        await TransacaoRetidaDeTeste.AguardarBloqueioAsync(Fixture.Factory.Services);
        await EsperarOPrazoAsync();
        await trava.ConfirmarAsync();

        return await disparada;
    }

    private static Task EsperarOPrazoAsync() => Task.Delay(MargemDoPrazo + TimeSpan.FromMilliseconds(500));

    private async Task<TransacaoRetidaDeTeste> TravarOPerfilAsync(Guid perfilId)
    {
        TransacaoRetidaDeTeste trava = await TransacaoRetidaDeTeste.AbrirAsync(Fixture.Factory.Services);
        await trava.Servicos.GetRequiredService<AutorizacaoDbContext>().Database.ExecuteSqlRawAsync(
            "SELECT 1 FROM autorizacao.perfis WHERE id = {0} FOR UPDATE", perfilId);

        return trava;
    }
}
