namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Idempotency;

using System.Net;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;

/// <summary>
/// A chave de idempotência reservada pelo filtro, com o TTL configurado com um resto de ticks e o relógio com a 7ª casa
/// decimal, expira num instante em microssegundos inteiros: o gravado no Postgres é o que decide a repetição, também no
/// limite exato.
/// </summary>
[Collection(ColecaoConfiguracaoComTempoEmResiduo.Name)]
[Trait("Category", "Integration")]
public sealed class TtlDaChaveComResiduoEndpointTests
{
    private readonly ConfiguracaoComTempoEmResiduoFixture _fixture;

    public TtlDaChaveComResiduoEndpointTests(ConfiguracaoComTempoEmResiduoFixture fixture) => _fixture = fixture;

    private ConfiguracaoComTempoEmResiduoFactory Fabrica => _fixture.Factory;

    private static DateTimeOffset InstanteComSetimaCasa()
    {
        DateTimeOffset agora = DateTimeOffset.UtcNow;
        return new DateTimeOffset(agora.UtcTicks - (agora.UtcTicks % TimeSpan.TicksPerSecond) + 1_234_567, TimeSpan.Zero);
    }

    private static DateTimeOffset ExpiracaoEsperada(DateTimeOffset reserva) =>
        new(reserva.UtcTicks - (reserva.UtcTicks % TimeSpan.TicksPerMicrosecond) + TimeSpan.TicksPerHour, TimeSpan.Zero);

    private async Task<HttpResponseMessage> EnviarAsync(DateTimeOffset instante, string sigla, string chave)
    {
        Fabrica.Relogio.ParaNo(instante);
        using HttpClient client = Fabrica.CreateClient();
        return await CampusHttpDeTeste.CriarAsync(client, sigla, chave);
    }

    private async Task<DateTimeOffset> ExpiracaoGravadaAsync(string chave)
    {
        await using AsyncServiceScope escopo = Fabrica.Services.CreateAsyncScope();
        ConfiguracaoDbContext db = escopo.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();
        return await db.Set<IdempotencyEntry>().AsNoTracking()
            .Where(e => e.IdempotencyKey == chave)
            .Select(e => e.ExpiresAt)
            .SingleAsync();
    }

    [Fact(DisplayName = "A chave reservada com TTL com resto de ticks expira em microssegundos inteiros no Postgres")]
    public async Task Reserva_TtlComResto_GravaAExpiracaoEmMicrossegundos()
    {
        DateTimeOffset reserva = InstanteComSetimaCasa();
        string chave = Guid.NewGuid().ToString();

        using HttpResponseMessage criacao = await EnviarAsync(reserva, CampusHttpDeTeste.NovaSigla(), chave);

        criacao.StatusCode.Should().Be(HttpStatusCode.Created);
        DateTimeOffset gravada = await ExpiracaoGravadaAsync(chave);
        gravada.Should().Be(ExpiracaoEsperada(reserva));
        (gravada.Ticks % TimeSpan.TicksPerMicrosecond).Should().Be(0);
    }

    [Fact(DisplayName = "Um microssegundo antes da expiração a mesma chave ainda repete a resposta")]
    public async Task Repeticao_UmMicrossegundoAntesDaExpiracao_RepeteAResposta()
    {
        DateTimeOffset reserva = InstanteComSetimaCasa();
        (string sigla, string chave) = (CampusHttpDeTeste.NovaSigla(), Guid.NewGuid().ToString());
        using HttpResponseMessage criacao = await EnviarAsync(reserva, sigla, chave);

        using HttpResponseMessage repeticao = await EnviarAsync(ExpiracaoEsperada(reserva).AddTicks(-10), sigla, chave);

        repeticao.StatusCode.Should().Be(HttpStatusCode.Created);
        repeticao.Headers.GetValues("Idempotency-Replayed").Should().ContainSingle().Which.Should().Be("true");
    }

    [Fact(DisplayName = "No instante exato da expiração a mesma chave já não repete a resposta")]
    public async Task Repeticao_NoInstanteExatoDaExpiracao_ExecutaDeNovo()
    {
        DateTimeOffset reserva = InstanteComSetimaCasa();
        (string sigla, string chave) = (CampusHttpDeTeste.NovaSigla(), Guid.NewGuid().ToString());
        using HttpResponseMessage criacao = await EnviarAsync(reserva, sigla, chave);

        using HttpResponseMessage repeticao = await EnviarAsync(ExpiracaoEsperada(reserva), sigla, chave);

        repeticao.Headers.Contains("Idempotency-Replayed").Should().BeFalse();
        repeticao.StatusCode.Should().Be(HttpStatusCode.Conflict, "a sigla já existe: a requisição foi executada de novo");
    }
}
