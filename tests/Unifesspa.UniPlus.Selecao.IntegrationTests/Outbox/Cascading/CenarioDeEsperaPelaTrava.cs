namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Globalization;
using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Host;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Assertions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// O host dos testes da espera pela trava do processo, com o limite da espera curto para a recusa vir logo, e o que todos
/// eles conferem da resposta 503: sem prazo de nova tentativa, a recusa guardada pela chave.
/// </summary>
internal sealed class CenarioDeEsperaPelaTrava : IAsyncDisposable
{
    internal static readonly TimeSpan LimiteDaEspera = TimeSpan.FromMilliseconds(300);
    internal static readonly TimeSpan EsperaDoTeste = TimeSpan.FromSeconds(20);

    private readonly CascadingFixture _fixture;

    private CenarioDeEsperaPelaTrava(CascadingFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _fixture = fixture;
        Host = fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuracao) =>
            configuracao.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TravaDoProcesso:EsperaMaxima"] = LimiteDaEspera.ToString("c", CultureInfo.InvariantCulture),
            })));
    }

    internal static CenarioDeEsperaPelaTrava Criar(CascadingFixture fixture) => new(fixture);

    /// <summary>O host com o limite curto, que atende as requisições que esperam pela trava.</summary>
    internal WebApplicationFactory<HostAssemblyMarker> Host { get; }

    /// <summary>O host sem o limite curto, para preparar os dados sem esperar por trava alguma.</summary>
    internal CascadingApiFactory Preparo => _fixture.Factory;

    internal IServiceProvider Services => Host.Services;

    internal async Task<Guid> SemearProcessoAsync(string nome)
    {
        await using AsyncServiceScope escopo = Preparo.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        (ProcessoSeletivo processo, _) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(db, new OpcoesDoProcessoSeletivoPublicavel { Nome = $"{nome} {Guid.CreateVersion7()}" });
        return processo.Id;
    }

    /// <summary>A recusa guardada: sem prazo, e o detalhe manda reler o estado e enviar com uma nova chave.</summary>
    internal static async Task ConferirEsperaEsgotadaGuardadaAsync(HttpResponseMessage resposta)
    {
        JsonElement problema = await LerEsperaEsgotadaAsync(resposta);

        resposta.Headers.RetryAfter.Should().BeNull("a recusa guardada não manda repetir com a mesma chave");
        problema.GetProperty("detail").GetString().Should().Contain("nova chave de idempotência");
        await resposta.AssertNoPiiAsync();
    }

    public async ValueTask DisposeAsync() => await Host.DisposeAsync();

    private static async Task<JsonElement> LerEsperaEsgotadaAsync(HttpResponseMessage resposta)
    {
        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable, corpo);
        using JsonDocument problema = JsonDocument.Parse(corpo);
        problema.RootElement.GetProperty("code").GetString().Should().Be("uniplus.concorrencia.espera_esgotada");
        return problema.RootElement.Clone();
    }
}
