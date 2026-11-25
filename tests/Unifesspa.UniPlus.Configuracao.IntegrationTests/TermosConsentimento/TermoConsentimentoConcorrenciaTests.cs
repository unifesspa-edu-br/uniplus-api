namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.TermosConsentimento;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Application.Commands.TermosConsentimento;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Kernel.Results;

using Wolverine;

/// <summary>
/// Prova de ponta a ponta do padrão canônico de concorrência otimista
/// (ADR-0119) para <c>RemoverTermoConsentimentoCommandHandler</c>: o handler
/// não captura <c>DbUpdateConcurrencyException</c> — se o
/// <c>SaveChangesAsync</c> automático do outbox (<c>AutoApplyTransactions</c>,
/// ADR-0004) tentasse rodar de novo sobre entidades ainda rastreadas, o lado
/// perdedor da corrida vazaria a exceção fora de qualquer catch em vez de
/// propagá-la limpa uma única vez.
/// </summary>
/// <remarks>
/// Mesma técnica determinística de <c>CalendarioDiasUteisConcorrenciaTests</c>
/// (lock de linha explícito via transação segurada, com <c>pg_locks</c>
/// provando o bloqueio em vez de um prazo fixo) — <c>Task.WhenAll</c> sem
/// sincronização não reproduz a corrida de forma confiável sob carga da suíte
/// completa (verificado empiricamente).
/// </remarks>
[Collection(ConfiguracaoEndpointCollection.Name)]
[Trait("Category", "Integration")]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class TermoConsentimentoConcorrenciaTests
{
    private readonly ConfiguracaoEndpointFixture _fixture;

    public TermoConsentimentoConcorrenciaTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName =
        "Remoção colidindo com uma escrita concorrente do mesmo termo propaga DbUpdateConcurrencyException limpa (sem segunda tentativa do outbox)")]
    public async Task Remocao_ColideComEscritaConcorrente_PropagaExcecaoLimpa()
    {
        MonolitoApiFactory api = _fixture.Factory;

        Guid id = await CriarTermoAsync(api);

        await RemoverDuranteEscritaConcorrenteAsync(api, id);
    }

    private static async Task RemoverDuranteEscritaConcorrenteAsync(MonolitoApiFactory api, Guid id)
    {
        await using AsyncServiceScope scopeB = api.Services.CreateAsyncScope();
        UniPlusDbContext dbB = scopeB.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        await using IDbContextTransaction txB = await dbB.Database.BeginTransactionAsync();

        int pidDbB = await BloquearTermoAsync(dbB, id);

        await ConferirRemocaoBloqueadaAsync(api, id, txB, pidDbB);
    }

    private static async Task ConferirRemocaoBloqueadaAsync(
        MonolitoApiFactory api, Guid id, IDbContextTransaction txB, int pidDbB)
    {
        await using AsyncServiceScope scopeA = api.Services.CreateAsyncScope();
        IMessageBus busA = scopeA.ServiceProvider.GetRequiredService<IMessageBus>();
        Task<Result> taskA = busA.InvokeAsync<Result>(new RemoverTermoConsentimentoCommand(id));
        await ConcorrenciaTestHelpers.WaitForBlockedBackendAsync(api, taskA, [pidDbB]);
        await txB.CommitAsync();

        await ConferirExcecaoDeConcorrenciaAsync(taskA);
        await ConferirTermoNaoRemovidoAsync(api, id);
    }

    private static async Task<Guid> CriarTermoAsync(MonolitoApiFactory api)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        TermoConsentimento criado = TermoConsentimento.Criar($"Termo LGPD {Guid.NewGuid():N}", null, null, null).Value!;
        db.TermosConsentimento.Add(criado);
        await db.SaveChangesAsync();
        return criado.Id;
    }

    private static async Task<int> BloquearTermoAsync(UniPlusDbContext db, Guid id)
    {
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE configuracao.termos_de_consentimento SET updated_at = now() WHERE id = {id}");
        return await ConcorrenciaTestHelpers.GetConnectionPidAsync(db);
    }

    private static async Task ConferirExcecaoDeConcorrenciaAsync(Task<Result> tarefa)
    {
        Func<Task> act = async () => await tarefa;
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>(
            "o xmin lido pelo handler ficou obsoleto assim que txB commitou a própria escrita na mesma linha");
    }

    private static async Task ConferirTermoNaoRemovidoAsync(MonolitoApiFactory api, Guid id)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        TermoConsentimento persistido = await db.TermosConsentimento.SingleAsync(t => t.Id == id);
        persistido.IsDeleted.Should().BeFalse(
            "o handler perdeu a corrida e não deve ter removido nada — nenhuma segunda tentativa do outbox deve ter reaplicado a remoção");
    }
}
