namespace Unifesspa.UniPlus.Infrastructure.Core.Caching;

using System.Text.Json;

using StackExchange.Redis;

public sealed class RedisCacheService : ICacheService
{
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
}
