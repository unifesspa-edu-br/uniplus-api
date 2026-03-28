namespace Unifesspa.UniPlus.Infrastructure.Common.Caching;

public interface ICacheService
{
    Task<T?> ObterAsync<T>(string chave, CancellationToken cancellationToken = default);
    Task DefinirAsync<T>(string chave, T valor, TimeSpan? expiracao = null, CancellationToken cancellationToken = default);
    Task RemoverAsync(string chave, CancellationToken cancellationToken = default);
}
