namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.CalendariosDiasUteis;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Application.Commands.CalendariosDiasUteis;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Kernel.Results;

using Wolverine;

/// <summary>
/// Prova de ponta a ponta do padrão canônico de concorrência otimista
/// (ADR-0119) para <c>RemoverCalendarioDiasUteisCommandHandler</c>: o handler
/// não captura <c>DbUpdateConcurrencyException</c> — se o
/// <c>SaveChangesAsync</c> automático do outbox (<c>AutoApplyTransactions</c>,
/// ADR-0004) tentasse rodar de novo sobre entidades ainda rastreadas, o lado
/// perdedor da corrida vazaria a exceção fora de qualquer catch em vez de
/// propagá-la limpa uma única vez.
/// </summary>
/// <remarks>
/// <c>Task.WhenAll</c> sem sincronização (o "safety net" de
/// <c>PrecedenciaFaseConcorrenciaTests</c>) não reproduz a corrida de forma
/// confiável aqui — sob a suíte completa, um dos dois lados pode atrasar o
/// bastante para que o outro já tenha commitado antes mesmo da sua própria
/// leitura, produzindo um 404 de negócio em vez de um conflito de xmin
/// (verificado empiricamente: passa isolado, falha sob carga da suíte). Este
/// teste força a corrida de forma determinística: uma transação explícita
/// (<c>ctxB</c>) segura o lock de linha do Postgres depois de já ter lido a
/// entidade; o handler real (<c>busA</c>) começa, lê a mesma linha (ainda sem
/// bloqueio de leitura) e bloqueia só na hora de escrever. Em vez de apostar
/// num prazo fixo, o teste faz poll em <c>pg_locks</c> até observar o backend
/// de <c>busA</c> realmente esperando pelo lock da linha — prova direta de
/// que ele já leu o xmin antigo e chegou à escrita, não uma inferência de
/// tempo. Só então libera <c>ctxB</c> com um UPDATE que já bate o xmin.
/// </remarks>
[Collection(ConfiguracaoEndpointCollection.Name)]
[Trait("Category", "Integration")]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class CalendarioDiasUteisConcorrenciaTests
{
    private readonly ConfiguracaoEndpointFixture _fixture;

    public CalendarioDiasUteisConcorrenciaTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName =
        "Remoção colidindo com uma escrita concorrente do mesmo dataset propaga DbUpdateConcurrencyException limpa (sem segunda tentativa do outbox)")]
    public async Task Remocao_ColideComEscritaConcorrente_PropagaExcecaoLimpa()
    {
        MonolitoApiFactory api = _fixture.Factory;

        Guid id;
        await using (AsyncServiceScope setupScope = api.Services.CreateAsyncScope())
        {
            ConfiguracaoDbContext db = setupScope.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();
            CalendarioDiasUteis criado = CalendarioDiasUteis.Criar(
                $"conc-{Guid.NewGuid():N}"[..20],
                [new DiaNaoUtilCriacao("NACIONAL", null, null, null, new DateOnly(2099, 1, 1), "Ano novo")]).Value!;
            db.CalendariosDiasUteis.Add(criado);
            await db.SaveChangesAsync();
            id = criado.Id;
        }

        await using AsyncServiceScope scopeB = api.Services.CreateAsyncScope();
        ConfiguracaoDbContext dbB = scopeB.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();
        await using IDbContextTransaction txB = await dbB.Database.BeginTransactionAsync();

        // Trava a linha (lock de escrita do Postgres) sem commitar ainda —
        // qualquer outra transação que tente escrever na mesma linha bloqueia
        // aqui até txB liberar.
        await dbB.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE configuracao.calendario_dias_uteis SET updated_at = now() WHERE id = {id}");
        int pidDbB = await ConcorrenciaTestHelpers.GetConnectionPidAsync(dbB);

        await using AsyncServiceScope scopeA = api.Services.CreateAsyncScope();
        IMessageBus busA = scopeA.ServiceProvider.GetRequiredService<IMessageBus>();
        Task<Result> taskA = busA.InvokeAsync<Result>(new RemoverCalendarioDiasUteisCommand(id));

        // Prova direta (não uma aposta de tempo) de que busA já leu o xmin
        // antigo e está bloqueado tentando escrever: poll em pg_stat_activity
        // até aparecer um backend em wait_event_type='Lock' que não é nem a
        // conexão de poll nem dbB (issue #1031 — identificação por PID, não
        // por texto de query).
        await ConcorrenciaTestHelpers.WaitForBlockedBackendAsync(api, taskA, [pidDbB]);

        await txB.CommitAsync();

        Func<Task> act = async () => await taskA;

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>(
            "o xmin lido pelo handler ficou obsoleto assim que txB commitou a própria escrita na mesma linha");

        await using AsyncServiceScope readScope = api.Services.CreateAsyncScope();
        ConfiguracaoDbContext readDb = readScope.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();
        CalendarioDiasUteis persistido = await readDb.CalendariosDiasUteis.SingleAsync(c => c.Id == id);
        persistido.IsDeleted.Should().BeFalse(
            "o handler perdeu a corrida e não deve ter removido nada — nenhuma segunda tentativa do outbox deve ter reaplicado a remoção");
    }

    /// <summary>
    /// Prova de ponta a ponta do achado de revisão do PR #1460 (api#1458): sem
    /// <c>RegistrarInclusaoDeDiaNaoUtil</c> marcar o pai como alterado, inserir só
    /// o filho nunca compararia o <c>xmin</c> do <see cref="CalendarioDiasUteis"/>
    /// — uma remoção (soft-delete) concorrente do MESMO dataset não viraria
    /// conflito, porque a FK só exige que a linha física do pai exista, não que
    /// ela esteja viva. O resultado seria uma linha <c>dia_nao_util</c>
    /// inalcançável sob um calendário já escondido.
    /// </summary>
    [Fact(DisplayName =
        "Inclusão colidindo com remoção concorrente do mesmo dataset propaga ConflitoDeConcorrencia sem inserir a data")]
    public async Task IncluirDiaNaoUtil_ColideComRemocaoConcorrente_RetornaConflitoSemInserir()
    {
        MonolitoApiFactory api = _fixture.Factory;
        var novaData = new DateOnly(2099, 6, 15);

        Guid id;
        await using (AsyncServiceScope setupScope = api.Services.CreateAsyncScope())
        {
            ConfiguracaoDbContext db = setupScope.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();
            CalendarioDiasUteis criado = CalendarioDiasUteis.Criar(
                $"conc-{Guid.NewGuid():N}"[..20],
                [new DiaNaoUtilCriacao("NACIONAL", null, null, null, new DateOnly(2099, 1, 1), "Ano novo")]).Value!;
            db.CalendariosDiasUteis.Add(criado);
            await db.SaveChangesAsync();
            id = criado.Id;
        }

        await using AsyncServiceScope scopeB = api.Services.CreateAsyncScope();
        ConfiguracaoDbContext dbB = scopeB.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();
        await using IDbContextTransaction txB = await dbB.Database.BeginTransactionAsync();

        // Simula o UPDATE que RemoverCalendarioDiasUteisCommandHandler (via
        // SoftDeleteInterceptor) emitiria, sem commitar ainda — trava a linha
        // do pai para qualquer outro escritor concorrente.
        await dbB.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE configuracao.calendario_dias_uteis SET is_deleted = true, updated_at = now() WHERE id = {id}");
        int pidDbB = await ConcorrenciaTestHelpers.GetConnectionPidAsync(dbB);

        await using AsyncServiceScope scopeA = api.Services.CreateAsyncScope();
        IMessageBus busA = scopeA.ServiceProvider.GetRequiredService<IMessageBus>();
        var itemA = new DiaNaoUtilCommandItem("NACIONAL", null, null, null, novaData, "Feriado incluído durante a corrida");
        Task<Result<CalendarioDiasUteisDto>> taskA = busA.InvokeAsync<Result<CalendarioDiasUteisDto>>(
            new IncluirDiaNaoUtilCommand(id, itemA));

        await ConcorrenciaTestHelpers.WaitForBlockedBackendAsync(api, taskA, [pidDbB]);

        await txB.CommitAsync();

        Result<CalendarioDiasUteisDto> resultadoA = await taskA;

        resultadoA.IsFailure.Should().BeTrue(
            "o xmin lido por busA ficou obsoleto assim que txB commitou o soft-delete na mesma linha");
        resultadoA.Error!.Code.Should().Be(CalendarioDiasUteisErrorCodes.ConflitoDeConcorrencia);

        await using AsyncServiceScope readScope = api.Services.CreateAsyncScope();
        ConfiguracaoDbContext readDb = readScope.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();
        int diasComANovaData = await readDb.Database.SqlQuery<int>(
            $"""
            SELECT count(*)::int AS "Value" FROM configuracao.dia_nao_util
            WHERE calendario_dias_uteis_id = {id} AND data = {novaData}
            """).SingleAsync();
        diasComANovaData.Should().Be(0,
            "busA perdeu a corrida e o INSERT do filho deve ter sido revertido junto com o UPDATE do pai que falhou");
    }

    /// <summary>
    /// Prova de ponta a ponta da Decisão D2 (api#1458): a checagem de duplicidade
    /// em memória de <c>CalendarioDiasUteis.IncluirDiaNaoUtil</c> só compara contra
    /// o que já está carregado no agregado — ela NÃO alcança uma linha inserida por
    /// outra transação ainda não commitada. A defesa real contra a corrida é o
    /// índice único <c>ix_dia_nao_util_unicidade</c> (sem token de concorrência no
    /// pai: inserir um filho não muta o pai, então nada aqui depende de <c>xmin</c>
    /// — ver <see cref="IncluirDiaNaoUtilCommandHandler"/>).
    /// </summary>
    [Fact(DisplayName =
        "Duas inclusões concorrentes da mesma combinação: uma persiste, a outra recebe DataDuplicadaNoDataset sem duplicar")]
    public async Task IncluirDiaNaoUtil_DuasInclusoesConcorrentesDaMesmaCombinacao_UmaFalhaSemDuplicar()
    {
        MonolitoApiFactory api = _fixture.Factory;
        var novaData = new DateOnly(2099, 5, 8);

        Guid id;
        await using (AsyncServiceScope setupScope = api.Services.CreateAsyncScope())
        {
            ConfiguracaoDbContext db = setupScope.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();
            CalendarioDiasUteis criado = CalendarioDiasUteis.Criar(
                $"conc-{Guid.NewGuid():N}"[..20],
                [new DiaNaoUtilCriacao("NACIONAL", null, null, null, new DateOnly(2099, 1, 1), "Ano novo")]).Value!;
            db.CalendariosDiasUteis.Add(criado);
            await db.SaveChangesAsync();
            id = criado.Id;
        }

        await using AsyncServiceScope scopeB = api.Services.CreateAsyncScope();
        ConfiguracaoDbContext dbB = scopeB.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();
        await using IDbContextTransaction txB = await dbB.Database.BeginTransactionAsync();

        // Insere a MESMA combinação diretamente por SQL cru, sem commitar — o
        // handler real (busA) não pode ver esta linha (ainda não commitada) e por
        // isso a checagem em memória do agregado não vai detectar a duplicata;
        // só o índice único, checado no momento do INSERT de busA, protege aqui.
        await dbB.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO configuracao.dia_nao_util
                (id, calendario_dias_uteis_id, abrangencia, municipio_ibge, uf, data, descricao, created_at)
            VALUES
                (gen_random_uuid(), {id}, 'ESTADUAL', NULL, 'PA', {novaData}, 'Feriado estadual (txB)', now())
            """);
        int pidDbB = await ConcorrenciaTestHelpers.GetConnectionPidAsync(dbB);

        await using AsyncServiceScope scopeA = api.Services.CreateAsyncScope();
        IMessageBus busA = scopeA.ServiceProvider.GetRequiredService<IMessageBus>();
        var itemA = new DiaNaoUtilCommandItem("ESTADUAL", null, null, null, novaData, "Feriado estadual (busA)", "PA");
        Task<Result<CalendarioDiasUteisDto>> taskA = busA.InvokeAsync<Result<CalendarioDiasUteisDto>>(
            new IncluirDiaNaoUtilCommand(id, itemA));

        await ConcorrenciaTestHelpers.WaitForBlockedBackendAsync(api, taskA, [pidDbB]);

        await txB.CommitAsync();

        Result<CalendarioDiasUteisDto> resultadoA = await taskA;

        resultadoA.IsFailure.Should().BeTrue(
            "busA só descobre o conflito no INSERT, depois que txB já commitou a mesma combinação");
        resultadoA.Error!.Code.Should().Be(CalendarioDiasUteisErrorCodes.DataDuplicadaNoDataset);

        await using AsyncServiceScope readScope = api.Services.CreateAsyncScope();
        ConfiguracaoDbContext readDb = readScope.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();
        int totalNaCombinacao = await readDb.Database.SqlQuery<int>(
            $"""
            SELECT count(*)::int AS "Value" FROM configuracao.dia_nao_util
            WHERE calendario_dias_uteis_id = {id} AND data = {novaData} AND abrangencia = 'ESTADUAL' AND uf = 'PA'
            """).SingleAsync();
        totalNaCombinacao.Should().Be(1, "só a linha de txB persiste — busA perdeu a corrida e nada seu foi gravado");
    }
}
