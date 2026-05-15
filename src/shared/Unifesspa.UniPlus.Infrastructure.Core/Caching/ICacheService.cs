namespace Unifesspa.UniPlus.Infrastructure.Core.Caching;

public interface ICacheService
{
    Task<T?> ObterAsync<T>(string chave, CancellationToken cancellationToken = default);
    Task DefinirAsync<T>(string chave, T valor, TimeSpan? expiracao = null, CancellationToken cancellationToken = default);
    Task RemoverAsync(string chave, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tenta adquirir um lease curto sobre <paramref name="chave"/> (pattern
    /// <c>SET NX EX</c>) para coordenar populadores concorrentes em cache miss
    /// — stampede protection (ADR-0056 §"Carve-out read-side").
    /// </summary>
    /// <param name="chave">Identificador do lease (convenção: <c>{recurso}:lease</c>).</param>
    /// <param name="ttl">Janela máxima de retenção do lease — protege contra leak por crash do caller.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>
    /// <see cref="IAsyncDisposable"/> que libera o lease no <c>DisposeAsync</c> via
    /// compare-and-delete (token aleatório próprio do caller — evita apagar lease alheio se
    /// o TTL expirou e outro caller adquiriu); ou <see langword="null"/> se outro caller já detém o lease.
    /// </returns>
    Task<IAsyncDisposable?> AcquireLeaseAsync(
        string chave,
        TimeSpan ttl,
        CancellationToken cancellationToken = default);
}
