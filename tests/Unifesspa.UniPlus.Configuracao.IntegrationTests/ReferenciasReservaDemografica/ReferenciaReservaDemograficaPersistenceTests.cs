namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.ReferenciasReservaDemografica;

using System.Linq.Expressions;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Application.Persistence;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// Integração ponta-a-ponta da Referência de reserva demográfica contra Postgres
/// real (UNI-REQ-0065): persistência dos percentuais, UNIQUE parcial de Censo,
/// liberação do slot por soft-delete, leitura cross-módulo e soft-delete
/// preservando a trilha.
/// </summary>
[Collection(ConfiguracaoDbCollection.Name)]
public sealed class ReferenciaReservaDemograficaPersistenceTests
{
    private const string AdminA = "admin-a";
    private const string AdminB = "admin-b";
    private const string BaseLegal = "Lei 12.711/2012, art. 10, III";

    private readonly ConfiguracaoDbFixture _fixture;

    public ReferenciaReservaDemograficaPersistenceTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Criar persiste os percentuais e fica visível pelo leitor cross-módulo")]
    public async Task Insert_PersisteEFicaVisivelPeloReader()
    {
        ReferenciaReservaDemografica referencia = Nova("2022", 78.50m, 1.20m, 8.40m);

        await SalvarReferencias(referencia);

        await using UniPlusDbContext readCtx = _fixture.CreateDbContext(userId: null);
        ReferenciaReservaDemografica persistida = await readCtx.ReferenciasReservaDemografica
            .SingleAsync(r => r.Id == referencia.Id);

        VerificarReferenciaPersistida(persistida);
        await VerificarReferenciaNoReader(readCtx, referencia.Id);
    }

    [Fact(DisplayName = "UNIQUE parcial (censo) rejeita segunda referência viva para o mesmo Censo")]
    public async Task UniquePartial_Censo_RejeitaDuplicataAtiva()
    {
        await SalvarReferencias(Nova("2000", 50m, 1m, 5m));

        await using UniPlusDbContext ctx2 = _fixture.CreateDbContext(AdminA);
        ctx2.ReferenciasReservaDemografica.Add(Nova("2000", 60m, 2m, 6m));

        Func<Task> act = async () => await ctx2.SaveChangesAsync();

        await VerificarConflitoDeCenso(act);
    }

    [Fact(DisplayName = "Soft-delete preserva a trilha e liberta o slot da UNIQUE parcial de Censo")]
    public async Task SoftDelete_PreservaTrilhaELibertaSlot()
    {
        ReferenciaReservaDemografica referencia = Nova("1991", 40m, 1m, 4m);
        await SalvarReferencias(referencia);
        await ExcluirReferencia(r => r.Id == referencia.Id);

        await VerificarTrilhaDeExclusao(referencia.Id);

        await using UniPlusDbContext ctx3 = _fixture.CreateDbContext(AdminA);
        ctx3.ReferenciasReservaDemografica.Add(Nova("1991", 41m, 2m, 5m));

        Func<Task> act = async () => await ctx3.SaveChangesAsync();
        await act.Should().NotThrowAsync("o slot do Censo foi liberado pelo soft-delete");
    }

    [Fact(DisplayName = "CHECK de banco rejeita percentual fora do intervalo via SQL cru")]
    public async Task Check_RejeitaPercentualForaDeFaixaViaSqlCru()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"INSERT INTO configuracao.referencias_demograficas (id, censo_referencia, ppi_percentual, quilombola_percentual, pcd_percentual, base_legal, created_at, is_deleted) VALUES ({Guid.CreateVersion7()}, {"9999"}, {120.0m}, {1.0m}, {5.0m}, {BaseLegal}, {DateTimeOffset.UtcNow}, {false})");

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "o CHECK ppi_percentual <= 100 impede o INSERT direto");
    }

    [Fact(DisplayName = "Reader.ListarVivasAsync ordena por Censo e exclui soft-deleted")]
    public async Task ListarVivas_OrdenaPorCensoEExcluiSoftDeleted()
    {
        // Prefixo único por execução: o banco é compartilhado na collection, então
        // filtramos o resultado às linhas deste teste para asserções determinísticas.
        string prefixo = Guid.NewGuid().ToString("N")[..12];
        string censoA = $"{prefixo}-a";
        string censoB = $"{prefixo}-b";
        string censoExcluido = $"{prefixo}-d";

        // Insere fora de ordem (B antes de A) para provar a ordenação do reader.
        await SalvarReferencias(Nova(censoB, 60m, 2m, 6m), Nova(censoA, 50m, 1m, 5m),
            Nova(censoExcluido, 40m, 1m, 4m));
        await ExcluirReferencia(r => r.CensoReferencia == censoExcluido);

        await VerificarReferenciasOrdenadas(prefixo, censoA, censoB);
    }

    private async Task SalvarReferencias(params ReferenciaReservaDemografica[] referencias)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(AdminA);
        ctx.ReferenciasReservaDemografica.AddRange(referencias);
        await ctx.SaveChangesAsync();
    }

    private async Task ExcluirReferencia(Expression<Func<ReferenciaReservaDemografica, bool>> criterio)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(AdminB);
        ReferenciaReservaDemografica referencia = await ctx.ReferenciasReservaDemografica
            .SingleAsync(criterio);
        ctx.ReferenciasReservaDemografica.Remove(referencia);
        await ctx.SaveChangesAsync();
    }

    private static void VerificarReferenciaPersistida(ReferenciaReservaDemografica persistida)
    {
        persistida.CensoReferencia.Should().Be("2022");
        persistida.PpiPercentual.Valor.Should().Be(78.50m);
        persistida.QuilombolaPercentual.Valor.Should().Be(1.20m);
        persistida.PcdPercentual.Valor.Should().Be(8.40m);
        persistida.CreatedBy.Should().Be(AdminA);
        persistida.IsDeleted.Should().BeFalse();
    }

    private static async Task VerificarReferenciaNoReader(UniPlusDbContext readCtx, Guid id)
    {
        var reader = new ReferenciaReservaDemograficaReader(readCtx);
        ReferenciaReservaDemograficaView? view = await reader.ObterPorIdAsync(id);
        view.Should().NotBeNull();
        view!.CensoReferencia.Should().Be("2022");
        view.PpiPercentual.Should().Be(78.50m);
    }

    private static async Task VerificarConflitoDeCenso(Func<Task> act)
    {
        // Trava as constantes que o handler usa para traduzir a corrida concorrente
        // (UniqueConstraintViolation.GetViolatedConstraint/IsCensoConflict) em
        // CensoJaExiste/409: SqlState 23505 + nome do índice único parcial.
        DbUpdateException ex = (await act.Should().ThrowAsync<DbUpdateException>()).Which;
        Npgsql.PostgresException pg = ex.InnerException.Should().BeOfType<Npgsql.PostgresException>().Which;
        pg.SqlState.Should().Be("23505");
        pg.ConstraintName.Should().Be(RestricoesDoBanco.ReferenciaDemograficaCensoAtivos);
    }

    private async Task VerificarTrilhaDeExclusao(Guid id)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        ReferenciaReservaDemografica excluida = await ctx.ReferenciasReservaDemografica
            .IgnoreQueryFilters().SingleAsync(r => r.Id == id);
        excluida.IsDeleted.Should().BeTrue();
        excluida.DeletedBy.Should().Be(AdminB);
    }

    private async Task VerificarReferenciasOrdenadas(string prefixo, string censoA, string censoB)
    {
        await using UniPlusDbContext readCtx = _fixture.CreateDbContext(userId: null);
        var reader = new ReferenciaReservaDemograficaReader(readCtx);
        IReadOnlyList<ReferenciaReservaDemograficaView> todas = await reader.ListarVivasAsync();

        string[] meus = [.. todas
            .Select(v => v.CensoReferencia)
            .Where(c => c.StartsWith(prefixo, StringComparison.Ordinal))];

        // O reader ordena por CensoReferencia ascendente e exclui o soft-deleted:
        // exatamente [censoA, censoB], nessa ordem (inserimos B antes de A).
        meus.Should().Equal([censoA, censoB]);
    }

    private static ReferenciaReservaDemografica Nova(string censo, decimal ppi, decimal quilombola, decimal pcd) =>
        ReferenciaReservaDemografica.Criar(censo, ppi, quilombola, pcd, BaseLegal).Value!;
}
