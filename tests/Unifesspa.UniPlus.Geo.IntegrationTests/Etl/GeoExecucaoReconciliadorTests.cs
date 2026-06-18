namespace Unifesspa.UniPlus.Geo.IntegrationTests.Etl;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Geo.Domain.Entities;
using Unifesspa.UniPlus.Geo.Infrastructure.Persistence;
using Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Etl;
using Unifesspa.UniPlus.Geo.IntegrationTests.Infrastructure;

/// <summary>
/// Reconciliação de execuções abandonadas (Story #674): só execuções <c>EmAndamento</c>
/// mais velhas que o limite de abandono são reclamadas como falha — uma carga recém-iniciada
/// (possivelmente ativa em outra réplica) é preservada. O índice único parcial impede dois
/// registros EmAndamento ao mesmo tempo, então os dois cenários são testados isoladamente.
/// </summary>
[Collection(GeoPostgisCollection.Name)]
public sealed class GeoExecucaoReconciliadorTests
{
    private static readonly TimeSpan Limite = TimeSpan.FromHours(6);

    private readonly GeoPostgisFixture _fixture;

    public GeoExecucaoReconciliadorTests(GeoPostgisFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Execução EmAndamento mais velha que o limite é reclamada como falha")]
    public async Task ExecucaoAntiga_Reclamada()
    {
        await LimparAsync();
        DateTimeOffset agora = TimeProvider.System.GetUtcNow();
        Guid id = await CriarEmAndamentoAsync(agora - TimeSpan.FromHours(10));

        int afetadas;
        await using (GeoDbContext ctx = _fixture.CreateDbContext())
        {
            afetadas = await GeoExecucaoReconciliador.ReclamarAbandonadasAsync(ctx, agora, Limite, CancellationToken.None);
        }

        afetadas.Should().Be(1);
        await using GeoDbContext leitura = _fixture.CreateDbContext();
        GeoImportacaoExecucao execucao = await leitura.ImportacaoExecucoes.SingleAsync(e => e.Id == id);
        execucao.Status.Should().Be(StatusImportacao.Falhou);
        execucao.ConcluidoEm.Should().NotBeNull();
    }

    [Fact(DisplayName = "Execução EmAndamento recente é preservada (não reclamada)")]
    public async Task ExecucaoRecente_Preservada()
    {
        await LimparAsync();
        DateTimeOffset agora = TimeProvider.System.GetUtcNow();
        Guid id = await CriarEmAndamentoAsync(agora - TimeSpan.FromMinutes(1));

        int afetadas;
        await using (GeoDbContext ctx = _fixture.CreateDbContext())
        {
            afetadas = await GeoExecucaoReconciliador.ReclamarAbandonadasAsync(ctx, agora, Limite, CancellationToken.None);
        }

        afetadas.Should().Be(0);
        await using GeoDbContext leitura = _fixture.CreateDbContext();
        GeoImportacaoExecucao execucao = await leitura.ImportacaoExecucoes.SingleAsync(e => e.Id == id);
        execucao.Status.Should().Be(StatusImportacao.EmAndamento);
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
