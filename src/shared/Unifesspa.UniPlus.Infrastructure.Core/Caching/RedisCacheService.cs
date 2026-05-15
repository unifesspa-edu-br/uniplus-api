namespace Unifesspa.UniPlus.Infrastructure.Core.Caching;

using System.Text.Json;

using StackExchange.Redis;

public sealed class RedisCacheService : ICacheService
{
    // Lua compare-and-delete: o release só apaga a chave se o valor armazenado
    // ainda for o token deste caller — proteção contra deletar lease alheio
    // após o TTL ter expirado e outro caller adquirido o slot.
    // Documentação: https://redis.io/docs/latest/develop/use/patterns/distributed-locks/
    private const string ReleaseLeaseLuaScript = @"
if redis.call('get', KEYS[1]) == ARGV[1] then
    return redis.call('del', KEYS[1])
else
    return 0
end";

    private readonly IDatabase _database;

    public RedisCacheService(IConnectionMultiplexer connectionMultiplexer)
    {
        ArgumentNullException.ThrowIfNull(connectionMultiplexer);
        _database = connectionMultiplexer.GetDatabase();
    }

    public async Task<T?> ObterAsync<T>(string chave, CancellationToken cancellationToken = default)
    {
        RedisValue valor = await _database.StringGetAsync(chave).ConfigureAwait(false);
        return valor.HasValue ? JsonSerializer.Deserialize<T>(valor.ToString()) : default;
    }

    public async Task DefinirAsync<T>(string chave, T valor, TimeSpan? expiracao = null, CancellationToken cancellationToken = default)
    {
        string json = JsonSerializer.Serialize(valor);
        if (expiracao.HasValue)
        {
            await _database.StringSetAsync(chave, json, new Expiration(expiracao.Value)).ConfigureAwait(false);
        }
        else
        {
            await _database.StringSetAsync(chave, json).ConfigureAwait(false);
        }
    }

    public async Task RemoverAsync(string chave, CancellationToken cancellationToken = default)
    {
        await _database.KeyDeleteAsync(chave).ConfigureAwait(false);
    }

    public async Task<IAsyncDisposable?> AcquireLeaseAsync(
        string chave,
        TimeSpan ttl,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(chave);
        if (ttl <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(ttl), "TTL do lease deve ser positivo.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        // Token único do caller — usado no compare-and-delete do release.
        string token = Guid.NewGuid().ToString("N");

        bool acquired = await _database.StringSetAsync(
            chave,
            token,
            ttl,
            When.NotExists)
            .ConfigureAwait(false);

        return acquired ? new RedisLeaseHandle(_database, chave, token) : null;
    }

    private sealed class RedisLeaseHandle(IDatabase database, string chave, string token) : IAsyncDisposable
    {
        private int _released;

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _released, 1) == 1)
            {
                return;
            }

            try
            {
                await database.ScriptEvaluateAsync(
                    ReleaseLeaseLuaScript,
                    [chave],
                    [token])
                    .ConfigureAwait(false);
            }
            catch (RedisException)
            {
                // Liberação best-effort: se Redis cair entre acquire e release,
                // o lease expira sozinho via TTL — não é necessário propagar a falha
                // (callers fazem flow de degradação na próxima leitura).
            }
        }
    }
}
