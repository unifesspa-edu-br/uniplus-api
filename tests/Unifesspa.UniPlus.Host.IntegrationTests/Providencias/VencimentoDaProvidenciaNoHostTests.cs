namespace Unifesspa.UniPlus.Host.IntegrationTests.Providencias;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Unifesspa.UniPlus.Host;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.Host.IntegrationTests.Perfis;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// O vencimento da concessão durante a tramitação deixa a providência prejudicada sem nada gravado (UNI-REQ-0376). O host
/// desta classe tem um relógio que o teste avança; a concessão vence no instante que ele lê, sem rotina.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
public sealed class VencimentoDaProvidenciaNoHostTests : ProvidenciasNoHostTestBase, IDisposable
{
    private readonly RelogioAvancavelDeTeste _relogio = new();
    private readonly WebApplicationFactory<HostAssemblyMarker> _host;

    public VencimentoDaProvidenciaNoHostTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
        _host = Fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(servicos =>
        {
            servicos.RemoveAll<TimeProvider>();
            servicos.AddSingleton<TimeProvider>(_relogio);
        }));
    }

    [Fact(DisplayName = "A concessão que vence durante a tramitação deixa a providência prejudicada na leitura, sem regravar a linha")]
    public async Task VencimentoDuranteATramitacao_PrejudicadaSemGravacao()
    {
        (Governanca g, Guid providencia, DateTimeOffset validaAte) = await ProvidenciaSobreConcessaoQueVenceAsync();
        LinhaDaProvidencia antes = await LinhaAsync(providencia);

        _relogio.Avancar(TimeSpan.FromHours(2));
        JsonElement lida = await ProvidenciaAsync(g.Diogo, providencia);

        (lida.GetProperty("situacao").GetString(), lida.GetProperty("encerramentoDaConcessao").GetProperty("causa").GetString()).Should().Be(("prejudicada", "vencida"));
        lida.GetProperty("encerramentoDaConcessao").GetProperty("em").GetDateTimeOffset().Should().BeCloseTo(validaAte, TimeSpan.FromMilliseconds(1));
        (await LinhaAsync(providencia)).Should().Be(antes, "a situação derivada não grava nada");
        (await FilaDasProvidenciasAsync(g.Diogo)).Should().NotContain(providencia);
        (await FilaDasProvidenciasAsync(g.Diogo, "?situacao=prejudicada")).Should().Contain(providencia);
    }

    [Fact(DisplayName = "A resposta que espera a trava da concessão e atravessa o vencimento é recusada, porque o instante é lido depois da trava")]
    public async Task RespostaQueAtravessaOVencimento_Recusada()
    {
        (Governanca g, Guid providencia, _) = await ProvidenciaSobreConcessaoQueVenceAsync();
        Guid concessao = (await ProvidenciaAsync(g.Diogo, providencia)).GetProperty("concessao").GetProperty("id").GetGuid();
        await using TransacaoRetidaDeTeste trava = await TransacaoRetidaDeTeste.AbrirAsync(Fixture.Factory.Services);
        await trava.Servicos.GetRequiredService<UniPlusDbContext>().Database.ExecuteSqlRawAsync(TravaDaConcessao, concessao);

        Task<HttpResponseMessage> resposta = ResponderAsync(g.Isabella, providencia);
        await trava.AguardarBloqueioAsync();
        _relogio.Avancar(TimeSpan.FromHours(2));
        await trava.ConfirmarAsync();

        await ConferirCodigoAsync(await resposta, HttpStatusCode.Conflict, "uniplus.autorizacao.providencia.concessao_nao_ativa");
        (await LinhaAsync(providencia)).Estado.Should().Be("aberta");
    }

    public void Dispose() => _host.Dispose();

    private protected override HttpClient NovoCliente() => _host.CreateClient();

    private async Task<(Governanca G, Guid Providencia, DateTimeOffset ValidaAte)> ProvidenciaSobreConcessaoQueVenceAsync()
    {
        _relogio.Reiniciar();
        Governanca g = await GovernancaAsync();
        DateTimeOffset validaAte = _relogio.GetUtcNow().AddHours(1);

        return (g, await SolicitarAsync(g.Cecilia, await ConcessaoDeLeandroAsync(validaAte)), validaAte);
    }
}
