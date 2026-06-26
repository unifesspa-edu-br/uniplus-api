namespace Unifesspa.UniPlus.Host.IntegrationTests.Infrastructure;

using System.Diagnostics.CodeAnalysis;

using Unifesspa.UniPlus.Infrastructure.Core.Caching;

/// <summary>
/// <see cref="ICacheService"/> de teste que NUNCA serve do cache: toda leitura
/// retorna miss e toda escrita é no-op. O lease é sempre concedido (no-op
/// disposable), levando o <c>UnidadeReader</c> direto à fonte sem o loop de
/// recheck.
/// </summary>
/// <remarks>
/// Prova-se a leitura cross-módulo <em>in-process</em> a partir do banco
/// único (schema <c>organizacao</c>) através de <see cref="Governance.Contracts.IUnidadeReader"/>.
/// O cache Redis que fica à frente do reader é ortogonal a essa prova — substituí-lo
/// por este fake mantém o teste focado no caminho DB in-process e dispensa um
/// container Redis. A durabilidade do cache real é coberta pelas suítes do módulo
/// OrganizacaoInstitucional.
/// </remarks>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via DI (AddScoped) em MonolitoHostApiFactory.")]
internal sealed class FakeInMemoryCacheService : ICacheService
{
    public Task<T?> ObterAsync<T>(string chave, CancellationToken cancellationToken = default) =>
        Task.FromResult<T?>(default);

    public Task DefinirAsync<T>(
        string chave,
        T valor,
        TimeSpan? expiracao = null,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task RemoverAsync(string chave, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<IAsyncDisposable?> AcquireLeaseAsync(
        string chave,
        TimeSpan ttl,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IAsyncDisposable?>(NoOpLease.Instance);

    private sealed class NoOpLease : IAsyncDisposable
    {
        public static readonly NoOpLease Instance = new();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
