namespace Unifesspa.UniPlus.Geo.IntegrationTests.Etl;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Geo.Domain.Entities;
using Unifesspa.UniPlus.Geo.Infrastructure.Persistence;
using Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Etl;
using Unifesspa.UniPlus.Geo.IntegrationTests.Infrastructure;

/// <summary>
/// Reconciliação de órfãs no startup aguardado (#695): o <see cref="GeoReconciliacaoStartupHostedService"/>
/// é um <c>IHostedService</c> cujo <c>StartAsync</c> conclui a reclamação ANTES de o host
/// aceitar disparos (registrado antes do worker e do seed). Cobre que uma órfã antiga é
/// marcada <c>Falhou</c> no <c>StartAsync</c> — liberando o índice único parcial para um novo
/// disparo, sem 409 espúrio — e que uma execução recente (possivelmente ativa em outra
/// réplica) é preservada. O índice único parcial impede dois registros EmAndamento ao mesmo
/// tempo, então os cenários são testados isoladamente.
/// </summary>
[Collection(GeoPostgisCollection.Name)]
public sealed class GeoReconciliacaoStartupHostedServiceTests
{
    private readonly GeoPostgisFixture _fixture;

    public GeoReconciliacaoStartupHostedServiceTests(GeoPostgisFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "#695: órfã EmAndamento antiga é reconciliada no StartAsync, liberando novos disparos (sem 409 espúrio)")]
    public async Task Startup_ReconciliaOrfaAntiga_E_LiberaDisparos()
    {
        await LimparAsync();
        DateTimeOffset agora = TimeProvider.System.GetUtcNow();
        Guid orfa = await CriarEmAndamentoAsync(agora - TimeSpan.FromHours(10));

        GeoReconciliacaoStartupHostedService hosted = CriarHostedService();
        await hosted.StartAsync(CancellationToken.None);

        await using GeoDbContext leitura = _fixture.CreateDbContext();
        GeoImportacaoExecucao execucao = await leitura.ImportacaoExecucoes.SingleAsync(e => e.Id == orfa);
        execucao.Status.Should().Be(StatusImportacao.Falhou, "a órfã antiga é reconciliada antes de aceitar disparos");
        execucao.ConcluidoEm.Should().NotBeNull();

        // Índice único parcial liberado: um novo disparo (mesma versão) não colide na UNIQUE —
        // sem a reconciliação aguardada, o seed/disparo imediato bateria 409 contra a órfã.
        Guid novo = await CriarEmAndamentoAsync(agora);
        novo.Should().NotBeEmpty();
    }

    [Fact(DisplayName = "#695: execução EmAndamento recente (possivelmente ativa em outra réplica) é preservada no StartAsync")]
    public async Task Startup_PreservaExecucaoRecente()
    {
        await LimparAsync();
        DateTimeOffset agora = TimeProvider.System.GetUtcNow();
        Guid recente = await CriarEmAndamentoAsync(agora - TimeSpan.FromMinutes(1));

        GeoReconciliacaoStartupHostedService hosted = CriarHostedService();
        await hosted.StartAsync(CancellationToken.None);

        await using GeoDbContext leitura = _fixture.CreateDbContext();
        GeoImportacaoExecucao execucao = await leitura.ImportacaoExecucoes.SingleAsync(e => e.Id == recente);
        execucao.Status.Should().Be(StatusImportacao.EmAndamento, "uma carga recente (sob o limite de abandono) não é reclamada");
    }

    private GeoReconciliacaoStartupHostedService CriarHostedService()
    {
        ServiceCollection services = new();
        services.AddScoped(_ => _fixture.CreateDbContext());
        ServiceProvider provider = services.BuildServiceProvider();

        return new GeoReconciliacaoStartupHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            TimeProvider.System,
            Options.Create(new EtlOpcoes()),
            NullLogger<GeoReconciliacaoStartupHostedService>.Instance);
    }

    private async Task<Guid> CriarEmAndamentoAsync(DateTimeOffset iniciadoEm)
    {
        await using GeoDbContext ctx = _fixture.CreateDbContext();
        GeoImportacaoExecucao execucao = GeoImportacaoExecucao.Iniciar("202601", "teste", iniciadoEm).Value!;
        ctx.ImportacaoExecucoes.Add(execucao);
        await ctx.SaveChangesAsync();
        return execucao.Id;
    }

    private async Task LimparAsync()
    {
        await using GeoDbContext ctx = _fixture.CreateDbContext();
        await ctx.Database.ExecuteSqlRawAsync("TRUNCATE TABLE geo_importacao_execucao");
    }
}
