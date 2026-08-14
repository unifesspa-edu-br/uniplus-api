namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.CalendariosDiasUteis;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

/// <summary>
/// Integração ponta-a-ponta do calendário de dias úteis contra Postgres real
/// (UNI-REQ-0116): persistência do dataset e dos dias não úteis (token de
/// <c>Abrangencia</c> via <c>AbrangenciaValueConverter</c>), índice único
/// parcial de vigência, CHECK de coerência de município e soft-delete
/// preservando os dias não úteis filhos (<c>ClientNoAction</c>).
/// </summary>
[Collection(ConfiguracaoDbCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class CalendarioDiasUteisPersistenceTests
{
    private const string AdminA = "admin-a";
    private const string AdminB = "admin-b";

    private readonly ConfiguracaoDbFixture _fixture;

    public CalendarioDiasUteisPersistenceTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Persistir com DiasNaoUteis e reler com Include devolve os dias corretos")]
    public async Task Insert_PersisteDiasNaoUteisComTokenCorreto()
    {
        CalendarioDiasUteis calendario = Nova(
            VersaoUnica(),
            new DiaNaoUtilCriacao("NACIONAL", null, null, null, new DateOnly(2027, 1, 1), "Confraternização Universal"),
            new DiaNaoUtilCriacao("ESTADUAL", null, null, null, new DateOnly(2027, 8, 15), "Adesão do Pará à Independência", "PA"),
            new DiaNaoUtilCriacao("MUNICIPAL", "1504208", "Marabá", "PA", new DateOnly(2027, 5, 8), "Aniversário de Marabá"));

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.CalendariosDiasUteis.Add(calendario);
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext readCtx = _fixture.CreateDbContext(userId: null);
        CalendarioDiasUteis persistido = await readCtx.CalendariosDiasUteis
            .Include(c => c.DiasNaoUteis)
            .SingleAsync(c => c.Id == calendario.Id);

        persistido.CreatedBy.Should().Be(AdminA);
        persistido.Vigente.Should().BeFalse();
        persistido.DiasNaoUteis.Should().HaveCount(3);
        persistido.DiasNaoUteis.Should().Contain(d =>
            d.Abrangencia == Abrangencia.Nacional && d.MunicipioIbge == null && d.Data == new DateOnly(2027, 1, 1));
        persistido.DiasNaoUteis.Should().Contain(d =>
            d.Abrangencia == Abrangencia.Estadual
            && d.MunicipioIbge == null
            && d.MunicipioNome == null
            && d.MunicipioUf == null
            && d.Uf == "PA"
            && d.Data == new DateOnly(2027, 8, 15));
        persistido.DiasNaoUteis.Should().Contain(d =>
            d.Abrangencia == Abrangencia.Municipal
            && d.MunicipioIbge == "1504208"
            && d.MunicipioNome == "Marabá"
            && d.MunicipioUf == "PA"
            && d.Uf == null
            && d.Data == new DateOnly(2027, 5, 8));
    }

    [Fact(DisplayName = "Exclusion constraint (vigente) rejeita um segundo dataset vigente")]
    public async Task ExclusionConstraint_Vigente_RejeitaSegundoVigenteAtivo()
    {
        CalendarioDiasUteis primeiro = Nova(VersaoUnica());
        primeiro.MarcarVigente();
        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.CalendariosDiasUteis.Add(primeiro);
            await ctx.SaveChangesAsync();
        }

        CalendarioDiasUteis segundo = Nova(VersaoUnica());
        segundo.MarcarVigente();
        await using ConfiguracaoDbContext ctx2 = _fixture.CreateDbContext(AdminA);
        ctx2.CalendariosDiasUteis.Add(segundo);

        Func<Task> act = async () => await ctx2.SaveChangesAsync();

        // ex_calendario_dias_uteis_vigente_unico é DEFERRABLE INITIALLY DEFERRED
        // (issue #1016 — SaveChangesAsync desmarca o vigente anterior e marca o novo
        // na mesma transação; um índice não-deferível colidiria por-statement mesmo
        // quando a transação termina num estado final válido). A checagem só roda no
        // COMMIT, então a violação chega como PostgresException bruta do
        // NpgsqlTransaction.Commit — não embrulhada em DbUpdateException, que o EF Core
        // só produz para falhas durante a execução do batch de comandos.
        Npgsql.PostgresException pg = (await act.Should().ThrowAsync<Npgsql.PostgresException>()).Which;
        pg.SqlState.Should().Be("23P01");
        pg.ConstraintName.Should().Be("ex_calendario_dias_uteis_vigente_unico");

        // "segundo" nunca commitou (transação abortada pela exclusion constraint), mas
        // "primeiro" ficou vigente=true na base compartilhada da fixture — desmarca para
        // não colidir com outros testes deste arquivo que também marcam vigente.
        await using ConfiguracaoDbContext cleanupCtx = _fixture.CreateDbContext(AdminA);
        CalendarioDiasUteis primeiroTracked = await cleanupCtx.CalendariosDiasUteis.SingleAsync(c => c.Id == primeiro.Id);
        primeiroTracked.MarcarNaoVigente();
        await cleanupCtx.SaveChangesAsync();
    }

    [Fact(DisplayName = "Sob transação ambiente, só ForcarChecagemImediataDeConstraints detecta a colisão")]
    public async Task ExclusionConstraint_SobTransacaoAmbiente_SoDetectaNaChecagemForcada()
    {
        CalendarioDiasUteis primeiro = Nova(VersaoUnica());
        primeiro.MarcarVigente();
        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.CalendariosDiasUteis.Add(primeiro);
            await ctx.SaveChangesAsync();
        }

        CalendarioDiasUteis segundo = Nova(VersaoUnica());
        segundo.MarcarVigente();
        await using ConfiguracaoDbContext ctx2 = _fixture.CreateDbContext(AdminA);

        // Abre a transação ANTES do SaveChanges — reproduz o outbox do Wolverine
        // (UseEntityFrameworkCoreTransactions + AutoApplyTransactions, ADR-0004), que
        // abre a transação ANTES do handler rodar e só comita DEPOIS dele retornar.
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx =
            await ctx2.Database.BeginTransactionAsync();
        ctx2.CalendariosDiasUteis.Add(segundo);

        // Sob transação já aberta, SaveChangesAsync só executa os comandos — quem
        // comita é o `using` da transação, não este método. A exclusion constraint
        // DEFERRED não é checada aqui: exatamente o cenário que fazia o handler
        // devolver 500 em vez de 409 (o commit real só rodaria no outbox do
        // Wolverine, fora do try/catch do handler).
        await ctx2.SaveChangesAsync();

        Func<Task> act = async () => await ctx2.ForcarChecagemImediataDeConstraintsAsync();

        Npgsql.PostgresException pg = (await act.Should().ThrowAsync<Npgsql.PostgresException>()).Which;
        pg.SqlState.Should().Be("23P01");
        pg.ConstraintName.Should().Be("ex_calendario_dias_uteis_vigente_unico");

        // "segundo" nunca commitou (a transação nunca chegou a Commit), mas "primeiro"
        // segue vigente=true na base compartilhada da fixture.
        await using ConfiguracaoDbContext cleanupCtx = _fixture.CreateDbContext(AdminA);
        CalendarioDiasUteis primeiroTracked = await cleanupCtx.CalendariosDiasUteis.SingleAsync(c => c.Id == primeiro.Id);
        primeiroTracked.MarcarNaoVigente();
        await cleanupCtx.SaveChangesAsync();
    }

    [Fact(DisplayName = "CHECK de banco rejeita dia municipal com snapshot parcial via SQL cru")]
    public async Task Check_RejeitaMunicipalComSnapshotParcialViaSqlCru()
    {
        CalendarioDiasUteis pai = Nova(VersaoUnica());
        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.CalendariosDiasUteis.Add(pai);
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext ctx2 = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await ctx2.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO configuracao.dia_nao_util
                 (id, calendario_dias_uteis_id, abrangencia, municipio_ibge, data, descricao, created_at)
             VALUES
                 ({Guid.CreateVersion7()}, {pai.Id}, {"MUNICIPAL"}, {"1504208"}, {new DateOnly(2027, 1, 1)}, {"Snapshot parcial"}, {DateTimeOffset.UtcNow})
             """);

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "o CHECK ck_dia_nao_util_municipio_coerente exige a tripla municipal completa");
    }

    [Fact(DisplayName = "CHECK de banco rejeita campo municipal fora de MUNICIPAL via SQL cru")]
    public async Task Check_RejeitaCampoMunicipalForaDeMunicipalViaSqlCru()
    {
        CalendarioDiasUteis pai = Nova(VersaoUnica());
        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.CalendariosDiasUteis.Add(pai);
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext ctx2 = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await ctx2.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO configuracao.dia_nao_util
                 (id, calendario_dias_uteis_id, abrangencia, municipio_nome, data, descricao, created_at)
             VALUES
                 ({Guid.CreateVersion7()}, {pai.Id}, {"NACIONAL"}, {"Marabá"}, {new DateOnly(2027, 1, 1)}, {"Campo indevido"}, {DateTimeOffset.UtcNow})
             """);

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "o CHECK ck_dia_nao_util_municipio_coerente proíbe cada campo municipal nas demais abrangências");
    }

    [Fact(DisplayName = "Soft-delete de dataset não vigente preserva os dias não úteis filhos (ClientNoAction)")]
    public async Task SoftDelete_PreservaDiasNaoUteisFilhos()
    {
        CalendarioDiasUteis calendario = Nova(
            VersaoUnica(),
            new DiaNaoUtilCriacao("NACIONAL", null, null, null, new DateOnly(2027, 1, 1), "Confraternização Universal"),
            new DiaNaoUtilCriacao("INSTITUCIONAL", null, null, null, new DateOnly(2027, 12, 24), "Recesso da Unifesspa"));

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.CalendariosDiasUteis.Add(calendario);
            await ctx.SaveChangesAsync();
        }

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminB))
        {
            CalendarioDiasUteis tracked = await ctx.CalendariosDiasUteis
                .SingleAsync(c => c.Id == calendario.Id);
            ctx.CalendariosDiasUteis.Remove(tracked);
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext readCtx = _fixture.CreateDbContext(userId: null);
        CalendarioDiasUteis excluido = await readCtx.CalendariosDiasUteis
            .IgnoreQueryFilters()
            .Include(c => c.DiasNaoUteis)
            .SingleAsync(c => c.Id == calendario.Id);

        excluido.IsDeleted.Should().BeTrue();
        excluido.DeletedBy.Should().Be(AdminB);
        excluido.DiasNaoUteis.Should().HaveCount(2, "os dias não úteis não têm soft-delete próprio e permanecem sob a linha soft-deleted");
    }

    [Fact(DisplayName = "ICalendarioVigenteReader preserva abrangência e município por dia")]
    public async Task VigenteReader_PreservaAbrangenciaEMunicipioPorDia()
    {
        CalendarioDiasUteis calendario = Nova(
            VersaoUnica(),
            new DiaNaoUtilCriacao("NACIONAL", null, null, null, new DateOnly(2027, 1, 1), "Confraternização Universal"),
            new DiaNaoUtilCriacao("MUNICIPAL", "1504208", "Marabá", "PA", new DateOnly(2027, 5, 8), "Aniversário de Marabá"));
        calendario.MarcarVigente();

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.CalendariosDiasUteis.Add(calendario);
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext readCtx = _fixture.CreateDbContext(userId: null);
        var reader = new CalendarioVigenteReader(readCtx);

        CalendarioVigenteView? vigente = await reader.ObterVigenteAsync();

        vigente.Should().NotBeNull();
        vigente!.Id.Should().Be(calendario.Id);
        vigente.DiasNaoUteis.Should().HaveCount(2);
        vigente.DiasNaoUteis.Should().Contain(d =>
            d.Data == new DateOnly(2027, 1, 1) && d.Abrangencia == "NACIONAL" && d.MunicipioIbge == null);
        vigente.DiasNaoUteis.Should().Contain(d =>
            d.Data == new DateOnly(2027, 5, 8)
            && d.Abrangencia == "MUNICIPAL"
            && d.MunicipioIbge == "1504208"
            && d.MunicipioNome == "Marabá"
            && d.MunicipioUf == "PA"
            && d.Uf == null);
    }

    private static string VersaoUnica() => $"cal-{Guid.NewGuid():N}"[..20];

    private static CalendarioDiasUteis Nova(string versaoDataset, params DiaNaoUtilCriacao[] dias)
    {
        DiaNaoUtilCriacao[] diasNaoUteis = dias.Length == 0
            ? [new DiaNaoUtilCriacao("NACIONAL", null, null, null, new DateOnly(2027, 1, 1), "Confraternização Universal")]
            : dias;
        return CalendarioDiasUteis.Criar(versaoDataset, diasNaoUteis).Value!;
    }
}
