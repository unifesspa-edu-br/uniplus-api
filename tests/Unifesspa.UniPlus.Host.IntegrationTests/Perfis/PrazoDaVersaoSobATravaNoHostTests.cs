namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Diagnostics.CodeAnalysis;
using System.Net;

using AwesomeAssertions;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Host;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;

/// <summary>
/// O instante de uma transição sobre o perfil é lido depois da trava: a requisição que espera a trava e atravessa o
/// prazo encontra a pendente vencida, e a rotina de expiração e a decisão sobre o mesmo perfil se resolvem por quem
/// trava primeiro.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class PrazoDaVersaoSobATravaNoHostTests : AprovacaoDaVersaoNoHostTestBase, IDisposable
{
    private static readonly TimeSpan MargemDoPrazo = TimeSpan.FromMinutes(1);

    private readonly RelogioAvancavelDeTeste _relogio = new();
    private readonly WebApplicationFactory<HostAssemblyMarker> _host;

    public PrazoDaVersaoSobATravaNoHostTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
        _host = Fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(servicos =>
        {
            servicos.RemoveAll<TimeProvider>();
            servicos.AddSingleton<TimeProvider>(_relogio);
        }));
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

    [Fact(DisplayName = "A decisão que trava primeiro expira a versão vencida e a rotina que vem depois não expira nada")]
    public async Task DecisaoTravaPrimeiro_ExpiraUmaSoVez()
    {
        (Envio vigente, Envio versao) = await VigenteEPendenteVerificadaAsync();
        using HttpClient gestao = await GestaoAsync();
        await ExpirarEmAsync(versao, MargemDoPrazo);
        await using TransacaoRetidaDeTeste trava = await TravarOPerfilAsync(vigente.PerfilId);

        Task<HttpResponseMessage> decisao = AprovarBrutoAsync(gestao, versao, EtapaGestao);
        await TransacaoRetidaDeTeste.AguardarBloqueioAsync(Fixture.Factory.Services);
        AtravessarOPrazo();
        Task<int> rotina = ExpirarAsync();
        await TransacaoRetidaDeTeste.AguardarBloqueioAsync(Fixture.Factory.Services, 2);
        await trava.ConfirmarAsync();

        using HttpResponseMessage resposta = await decisao;
        await ConferirRecusaDoPerfilAsync(resposta, HttpStatusCode.UnprocessableEntity, "versao_expirada");
        (await rotina).Should().Be(0);
        await ConferirVersaoSoExpiradaAsync(vigente, versao);
    }

    [Fact(DisplayName = "A rotina que trava primeiro expira a versão vencida e a decisão que vem depois a encontra expirada")]
    public async Task RotinaTravaPrimeiro_ADecisaoEncontraAVersaoExpirada()
    {
        (Envio vigente, Envio versao) = await VigenteEPendenteVerificadaAsync();
        using HttpClient gestao = await GestaoAsync();
        await ExpirarEmAsync(versao, MargemDoPrazo);
        AtravessarOPrazo();
        await using TransacaoRetidaDeTeste trava = await TravarOPerfilAsync(vigente.PerfilId);

        Task<int> rotina = ExpirarAsync();
        await TransacaoRetidaDeTeste.AguardarBloqueioAsync(Fixture.Factory.Services);
        Task<HttpResponseMessage> decisao = AprovarBrutoAsync(gestao, versao, EtapaGestao);
        await TransacaoRetidaDeTeste.AguardarBloqueioAsync(Fixture.Factory.Services, 2);
        await trava.ConfirmarAsync();

        (await rotina).Should().Be(1);
        using HttpResponseMessage resposta = await decisao;
        await ConferirRecusaDoPerfilAsync(resposta, HttpStatusCode.UnprocessableEntity, "versao_nao_pendente");
        await ConferirVersaoSoExpiradaAsync(vigente, versao);
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
            "UPDATE autorizacao.versoes_do_perfil SET expira_em = {0} WHERE id = {1}", _relogio.GetUtcNow() + prazo, versao.VersaoId);

    private async Task<HttpResponseMessage> ChamarAtravessandoOPrazoAsync(Guid perfilId, Func<Task<HttpResponseMessage>> chamada)
    {
        await using TransacaoRetidaDeTeste trava = await TravarOPerfilAsync(perfilId);
        Task<HttpResponseMessage> disparada = chamada();
        await TransacaoRetidaDeTeste.AguardarBloqueioAsync(Fixture.Factory.Services);
        AtravessarOPrazo();
        await trava.ConfirmarAsync();

        return await disparada;
    }

    private void AtravessarOPrazo() => _relogio.Avancar(MargemDoPrazo + TimeSpan.FromSeconds(30));

    private async Task<TransacaoRetidaDeTeste> TravarOPerfilAsync(Guid perfilId)
    {
        TransacaoRetidaDeTeste trava = await TransacaoRetidaDeTeste.AbrirAsync(Fixture.Factory.Services);
        await trava.Servicos.GetRequiredService<AutorizacaoDbContext>().Database.ExecuteSqlRawAsync(
            "SELECT 1 FROM autorizacao.perfis WHERE id = {0} FOR UPDATE", perfilId);

        return trava;
    }

    private async Task<int> ExpirarAsync()
    {
        await using AsyncServiceScope escopo = _host.Services.CreateAsyncScope();

        return await escopo.ServiceProvider.GetRequiredService<ExpiracaoDasVersoesPendentes>().ExecutarAsync(CancellationToken.None);
    }

    private async Task ConferirVersaoSoExpiradaAsync(Envio vigente, Envio versao)
    {
        (await SituacaoDaVersaoNoBancoAsync(versao.VersaoId)).Should().Be("expirada");
        (await DecisoesAsync(versao.VersaoId)).Should().BeEmpty();
        (await VigenciasAsync(vigente.PerfilId)).Should().ContainSingle(v => v.Aberta && v.VersaoId == vigente.VersaoId);
    }

    public void Dispose() => _host.Dispose();

    private protected override HttpClient NovoCliente() => _host.CreateClient();
}
