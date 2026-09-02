namespace Unifesspa.UniPlus.Discentes.UnitTests.Sincronizacao;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Discentes.Application.Abstractions;
using Unifesspa.UniPlus.Discentes.Domain.Entities;
using Unifesspa.UniPlus.Discentes.Domain.Interfaces;
using Unifesspa.UniPlus.Discentes.Infrastructure.Sincronizacao;

/// <summary>
/// O gravador é o ponto onde a falha de um lote precisa parar.
/// </summary>
/// <remarks>
/// Acima dele está a varredura das duas consultas à origem; se uma falha escapar daqui,
/// ela encerra a varredura inteira e todos os lotes seguintes deixam de ser tentados por
/// causa de um único vínculo problemático.
/// </remarks>
public sealed class GravadorDeVinculosTests
{
    [Fact]
    public async Task Falha_ao_preparar_o_lote_nao_escapa_do_gravador()
    {
        // Cifrar o CPF de um vínculo alterado acontece na preparação, antes da confirmação,
        // e pode falhar por conta própria.
        GravadorDeVinculos gravador = Montar(new RepositorioQueFalhaAoPreparar());

        DesfechoDaGravacao desfecho = await gravador.GravarAsync([]);

        desfecho.Falha.Should().NotBeNull(
            "a falha da preparação precisa ser devolvida, e não propagada para a varredura");
        desfecho.Classificacao.Escritos.Should().Be(0, "nada foi gravado");
    }

    [Fact]
    public async Task Cancelamento_continua_subindo()
    {
        // Cancelamento não é falha de lote: é ordem de parar, e quem varre precisa recebê-la.
        GravadorDeVinculos gravador = Montar(new RepositorioCancelado());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await gravador.GravarAsync([]));
    }

    private static GravadorDeVinculos Montar(IVinculoDiscenteRepository repositorio)
    {
        ServiceCollection servicos = new();
        servicos.AddScoped(_ => repositorio);
        servicos.AddScoped<IDiscentesUnitOfWork>(_ => new UnidadeDeTrabalhoInerte());

        return new GravadorDeVinculos(
            servicos.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>());
    }

    private sealed class RepositorioQueFalhaAoPreparar : RepositorioInerte
    {
        public override Task<ResultadoDaGravacao> GravarLoteAsync(
            IReadOnlyList<VinculoSincronizavel> lote, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Falha ao cifrar o CPF do vínculo.");
    }

    private sealed class RepositorioCancelado : RepositorioInerte
    {
        public override Task<ResultadoDaGravacao> GravarLoteAsync(
            IReadOnlyList<VinculoSincronizavel> lote, CancellationToken cancellationToken = default) =>
            throw new OperationCanceledException();
    }

    private abstract class RepositorioInerte : IVinculoDiscenteRepository
    {
        public virtual Task<ResultadoDaGravacao> GravarLoteAsync(
            IReadOnlyList<VinculoSincronizavel> lote, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ResultadoDaGravacao(0, 0, 0));

        public Task<VinculoDiscente?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<VinculoDiscente?>(null);

        public Task<VinculoDiscente?> ObterPorIdSigaaAsync(long idDiscenteSigaa, CancellationToken cancellationToken = default) =>
            Task.FromResult<VinculoDiscente?>(null);

        public Task AdicionarAsync(VinculoDiscente entity, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task AtualizarAsync(VinculoDiscente entity, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class UnidadeDeTrabalhoInerte : IDiscentesUnitOfWork
    {
        public Task<int> SalvarAlteracoesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(0);
    }
}
