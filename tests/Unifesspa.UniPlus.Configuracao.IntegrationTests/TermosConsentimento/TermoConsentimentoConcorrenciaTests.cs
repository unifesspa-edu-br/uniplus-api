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

        Guid id;
        await using (AsyncServiceScope setupScope = api.Services.CreateAsyncScope())
        {
            ConfiguracaoDbContext db = setupScope.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();
            TermoConsentimento criado = TermoConsentimento.Criar("Termo LGPD", null, null, null).Value!;
            db.TermosConsentimento.Add(criado);
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
            $"UPDATE configuracao.termo_consentimento SET updated_at = now() WHERE id = {id}");
        int pidDbB = await ConcorrenciaTestHelpers.ObterPidDaConexaoAsync(dbB);

        await using AsyncServiceScope scopeA = api.Services.CreateAsyncScope();
        IMessageBus busA = scopeA.ServiceProvider.GetRequiredService<IMessageBus>();
        Task<Result> taskA = busA.InvokeAsync<Result>(new RemoverTermoConsentimentoCommand(id));

        // Prova direta (não uma aposta de tempo) de que busA já leu o xmin
        // antigo e está bloqueado tentando escrever: poll em pg_stat_activity
        // até aparecer um backend em wait_event_type='Lock' que não é nem a
        // conexão de poll nem dbB (issue #1031 — identificação por PID, não
        // por texto de query).
        await ConcorrenciaTestHelpers.AguardarBackendBloqueadoAsync(api, taskA, [pidDbB]);

        await txB.CommitAsync();

        Func<Task> act = async () => await taskA;

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>(
            "o xmin lido pelo handler ficou obsoleto assim que txB commitou a própria escrita na mesma linha");

        await using AsyncServiceScope readScope = api.Services.CreateAsyncScope();
        ConfiguracaoDbContext readDb = readScope.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();
        TermoConsentimento persistido = await readDb.TermosConsentimento.SingleAsync(t => t.Id == id);
        persistido.IsDeleted.Should().BeFalse(
            "o handler perdeu a corrida e não deve ter removido nada — nenhuma segunda tentativa do outbox deve ter reaplicado a remoção");
    }
}
