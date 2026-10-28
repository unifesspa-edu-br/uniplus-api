namespace Unifesspa.UniPlus.Discentes.IntegrationTests;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using Unifesspa.UniPlus.Discentes.Infrastructure.Persistence;
using Unifesspa.UniPlus.Discentes.Infrastructure.Sincronizacao;

[Trait("Category", "Integration")]
public sealed class SincronizacaoDiariaHealthCheckTests : IClassFixture<VinculoDiscenteDbFixture>
{
    private const short Concluida = 4;
    private const short Parcial = 5;
    private const short Falhou = 6;

    private readonly VinculoDiscenteDbFixture _fixture;

    public SincronizacaoDiariaHealthCheckTests(VinculoDiscenteDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Sem_nenhuma_execucao_esta_saudavel()
    {
        await using DiscentesDbContext db = await LimparAsync();

        (await VerificarAsync(db)).Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task Ultima_execucao_concluida_esta_saudavel()
    {
        await using DiscentesDbContext db = await LimparAsync();
        await ReservarAsync(db, new DateOnly(2092, 5, 4), Concluida);

        (await VerificarAsync(db)).Status.Should().Be(HealthStatus.Healthy);
    }

    [Theory]
    [InlineData(Parcial)]
    [InlineData(Falhou)]
    public async Task Ultima_execucao_parcial_ou_com_falha_esta_degradada(short estado)
    {
        await using DiscentesDbContext db = await LimparAsync();
        await ReservarAsync(db, new DateOnly(2092, 5, 4), estado);

        (await VerificarAsync(db)).Status.Should().Be(HealthStatus.Degraded);
    }

    [Fact]
    public async Task Dia_sem_execucao_alertado_depois_da_ultima_reserva_esta_degradado()
    {
        await using DiscentesDbContext db = await LimparAsync();
        await ReservarAsync(db, new DateOnly(2092, 5, 4), Concluida);
        await AlertarAsync(db, new DateOnly(2092, 5, 5));

        (await VerificarAsync(db)).Status.Should().Be(HealthStatus.Degraded);
    }

    [Fact]
    public async Task Execucao_concluida_depois_da_falha_volta_a_ficar_saudavel()
    {
        await using DiscentesDbContext db = await LimparAsync();
        await ReservarAsync(db, new DateOnly(2092, 5, 4), Falhou);
        await AlertarAsync(db, new DateOnly(2092, 5, 5));
        await ReservarAsync(db, new DateOnly(2092, 5, 6), Concluida);

        (await VerificarAsync(db)).Status.Should().Be(HealthStatus.Healthy);
    }

    private static Task<HealthCheckResult> VerificarAsync(DiscentesDbContext db) =>
        new SincronizacaoDiariaHealthCheck(db).CheckHealthAsync(new HealthCheckContext());

    private static Task<int> ReservarAsync(DiscentesDbContext db, DateOnly dia, short estado) =>
        db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO discentes.sincronizacao_diaria (data_de_referencia, estado, agendada_em)
            VALUES ({dia}, {estado}, {DateTimeOffset.UtcNow})
            """);

    private static Task<int> AlertarAsync(DiscentesDbContext db, DateOnly dia) =>
        db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO discentes.alerta_sincronizacao_diaria (data_de_referencia, emitida_em)
            VALUES ({dia}, {DateTimeOffset.UtcNow})
            """);

    private async Task<DiscentesDbContext> LimparAsync()
    {
        DiscentesDbContext db = _fixture.CreateDbContext();
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE discentes.sincronizacao_diaria, discentes.alerta_sincronizacao_diaria");
        return db;
    }
}
