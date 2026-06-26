namespace Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

using System.Diagnostics.CodeAnalysis;

using Unifesspa.UniPlus.Infrastructure.Core.Caching;

/// <summary>
/// <see cref="ICacheService"/> de teste que NUNCA serve do cache: toda leitura
/// retorna miss e toda escrita é no-op; o lease é sempre concedido (no-op
/// disposable), levando os readers direto à fonte sem o loop de recheck.
/// </summary>
/// <remarks>
/// Substitui o <c>RedisCacheService</c> nas suítes de integração que sobem a API
/// UniPlus, dispensando um container Redis — o caminho exercitado é o DB
/// in-process. A durabilidade do cache real é coberta pelas suítes do módulo
/// OrganizacaoInstitucional.
/// </remarks>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via DI (AddScoped) em MonolitoApiFactory.")]
public sealed class FakeInMemoryCacheService : ICacheService
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
