namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

/// <summary>
/// A retificação em um ato só tem como alvo a versão que vale no momento em que ela executa, e não uma que a requisição
/// nomeie. Se a requisição perde a espera pela trava e a repetição com a mesma chave executasse, ela retificaria a
/// versão do vencedor, e um ato que ninguém pediu entraria no registro. Por isso a recusa por espera esgotada fica guardada
/// pela chave, sem Retry-After: a mesma chave devolve a mesma recusa, e só uma nova chave executa.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
public sealed class EsperaPelaTravaNaRetificacaoEndpointTests : IAsyncLifetime
{
    private readonly CascadingFixture _fixture;
    private readonly CenarioDeEsperaPelaTrava _cenario;

    public EsperaPelaTravaNaRetificacaoEndpointTests(CascadingFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _fixture = fixture;
        _cenario = CenarioDeEsperaPelaTrava.Criar(fixture);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _cenario.DisposeAsync();

    [Fact(DisplayName = "A retificação em um ato só que perde a espera pela trava responde 503 sem Retry-After e guarda a recusa: a mesma chave não executa, e uma nova chave retifica uma só vez")]
    public async Task Retificacao_SobTravaRetida_GuardaARecusa_ENovaChaveExecutaUmaVez()
    {
        (Guid processoId, Guid documentoDaRetificacao) = await PublicarProcessoAsync();
        string chave = NovaChave();
        using HttpClient cliente = _cenario.Host.CreateClient();

        await RecusarEnquantoOutraOperacaoRetemOProcessoAsync(cliente, processoId, documentoDaRetificacao, chave);

        await ConferirQueAMesmaChaveRecebeARecusaGuardadaAsync(cliente, processoId, documentoDaRetificacao, chave);
        await ConferirQueUmaNovaChaveRetificaUmaSoVezAsync(cliente, processoId, documentoDaRetificacao);
    }

    private async Task RecusarEnquantoOutraOperacaoRetemOProcessoAsync(HttpClient cliente, Guid processoId, Guid documentoId, string chave)
    {
        await using ConexaoQueTravaOProcesso outraOperacao = await ConexaoQueTravaOProcesso.TravarAsync(_cenario.Services, processoId);

        using HttpResponseMessage recusada = await RetificarAsync(cliente, processoId, documentoId, chave);

        await CenarioDeEsperaPelaTrava.ConferirEsperaEsgotadaGuardadaAsync(recusada);
        await outraOperacao.DesfazerAsync();
    }

    private async Task ConferirQueAMesmaChaveRecebeARecusaGuardadaAsync(HttpClient cliente, Guid processoId, Guid documentoId, string chave)
    {
        using HttpResponseMessage repeticao = await RetificarAsync(cliente, processoId, documentoId, chave);

        await CenarioDeEsperaPelaTrava.ConferirEsperaEsgotadaGuardadaAsync(repeticao);
        repeticao.Headers.GetValues("Idempotency-Replayed").Should().ContainSingle().Which.Should().Be("true");
        (await VersoesDoProcessoAsync(processoId)).Should().Be(1, "a repetição com a mesma chave devolveu a recusa guardada e não executou");
    }

    private async Task ConferirQueUmaNovaChaveRetificaUmaSoVezAsync(HttpClient cliente, Guid processoId, Guid documentoId)
    {
        using HttpResponseMessage novaChave = await RetificarAsync(cliente, processoId, documentoId, NovaChave());

        novaChave.StatusCode.Should().Be(HttpStatusCode.NoContent, await novaChave.Content.ReadAsStringAsync());
        (await VersoesDoProcessoAsync(processoId)).Should().Be(2, "a nova chave retifica o estado que o cliente releu");
    }

    private static string NovaChave() => Guid.CreateVersion7().ToString("N");

    private async Task<(Guid ProcessoId, Guid DocumentoDaRetificacao)> PublicarProcessoAsync()
    {
        await TiposDeAtoSeeder.SemearAsync(_fixture.Factory.Services);
        (Guid processoId, Guid documentoDaAbertura) = await PublicacaoPeloEndpointDeTeste
            .SemearProcessoAsync(_fixture.Factory, nameof(EsperaPelaTravaNaRetificacaoEndpointTests));
        using HttpClient cliente = _fixture.Factory.CreateClient();
        using HttpResponseMessage publicacao = await PublicacaoPeloEndpointDeTeste.PublicarAsync(cliente, processoId, documentoDaAbertura);
        publicacao.StatusCode.Should().Be(HttpStatusCode.NoContent, await publicacao.Content.ReadAsStringAsync());
        Guid documentoDaRetificacao = await PublicacaoPeloEndpointDeTeste.SemearDocumentoConfirmadoAsync(_fixture.Factory, processoId);
        return (processoId, documentoDaRetificacao);
    }

    private static Task<HttpResponseMessage> RetificarAsync(HttpClient cliente, Guid processoId, Guid documentoId, string chave) =>
        PublicacaoPeloEndpointDeTeste.RetificarAsync(cliente, processoId, documentoId, chaveDeIdempotencia: chave).WaitAsync(CenarioDeEsperaPelaTrava.EsperaDoTeste);

    private async Task<int> VersoesDoProcessoAsync(Guid processoId)
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        return await db.VersoesConfiguracao.AsNoTracking().CountAsync(v => v.ProcessoSeletivoId == processoId);
    }
}
