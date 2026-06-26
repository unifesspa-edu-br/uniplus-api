namespace Unifesspa.UniPlus.Geo.Infrastructure.Cep;

using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Geo.Application.Abstractions;
using Unifesspa.UniPlus.Geo.Application.DTOs;
using Unifesspa.UniPlus.Geo.Infrastructure.Caching;
using Unifesspa.UniPlus.Infrastructure.Core.Caching;

/// <summary>
/// Resolve CEP com cache-aside (Redis) sobre o <see cref="ICepReader"/>. A chave
/// compõe-se com o selo de versão vigente (<see cref="RedisGeoCepCacheInvalidador.ChaveSeloVersaoVigente"/>,
/// #674): <c>geo:cep:v{versao}:{cep}</c>. Quando o ETL sela uma nova release, o selo
/// muda e as entradas antigas viram inalcançáveis (invalidação O(1), sem SCAN). Só
/// resolução positiva (200) é cacheada; 404 nunca — a ausência não pode congelar
/// caso a base seja recarregada/enriquecida.
/// </summary>
/// <remarks>
/// O <see cref="ICacheService"/> entra via <see cref="Lazy{T}"/> de propósito: o
/// <c>IConnectionMultiplexer</c> conecta na construção, e diferir essa resolução
/// (capturando a falha) garante que o lookup <strong>degrade para o banco</strong>
/// quando o Redis está fora — a resolução não depende do cache estar de pé. Espelha
/// o padrão do <c>GeoEtlOrquestrador</c> (#674).
/// </remarks>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via DI em GeoInfrastructureRegistration.")]
public sealed partial class CepResolver : ICepResolver
{
    // Chave da memoização em processo do selo de versão vigente (#703). Única (um selo
    // global por instância de cache em memória), distinta das chaves de CEP do Redis.
    private const string ChaveMemoSelo = "geo:cep:selo-vigente-memo";

    private readonly Lazy<ICacheService> _cache;
    private readonly ICepReader _reader;
    private readonly IMemoryCache _memoria;
    private readonly TimeProvider _relogio;
    private readonly TimeSpan _ttl;
    private readonly TimeSpan _seloTtl;
    private readonly ILogger<CepResolver> _logger;

    public CepResolver(
        Lazy<ICacheService> cache,
        ICepReader reader,
        IMemoryCache memoria,
        TimeProvider relogio,
        IOptions<GeoCepCacheOptions> opcoes,
        ILogger<CepResolver> logger)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(memoria);
        ArgumentNullException.ThrowIfNull(relogio);
        ArgumentNullException.ThrowIfNull(opcoes);
        ArgumentNullException.ThrowIfNull(logger);
        _cache = cache;
        _reader = reader;
        _memoria = memoria;
        _relogio = relogio;
        _ttl = opcoes.Value.Ttl;
        _seloTtl = opcoes.Value.SeloTtl;
        _logger = logger;
    }

    public async Task<CepResolvidoDto?> ResolverAsync(string cep, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cep);

        // Resolve o cache de forma resiliente (Lazy + try/catch): Redis fora → null → banco.
        ICacheService? cache = ResolverCacheOuNulo();
        string? chave = cache is null
            ? null
            : await ResolverChaveAsync(cache, cep, cancellationToken).ConfigureAwait(false);

        if (cache is not null && chave is not null)
        {
            CepResolvidoDto? cacheado = await TentarLerCacheAsync(cache, chave, cancellationToken).ConfigureAwait(false);
            if (cacheado is not null)
            {
                return cacheado;
            }
        }

        CepResolvidoDto? resolvido = await _reader.ResolverAsync(cep, cancellationToken).ConfigureAwait(false);
        if (resolvido is null)
        {
            // 404 não é cacheado — só resolução positiva entra no Redis.
            return null;
        }

        if (cache is not null && chave is not null)
        {
            await TentarGravarCacheAsync(cache, chave, resolvido, cancellationToken).ConfigureAwait(false);
        }

        return resolvido;
    }

    private ICacheService? ResolverCacheOuNulo()
    {
        try
        {
            return _cache.Value;
        }
        catch (Exception excecao) when (excecao is not OperationCanceledException)
        {
            // Redis indisponível (o .Value resolve o Connect do IConnectionMultiplexer).
            LogCacheIndisponivel(_logger, excecao);
            return null;
        }
    }

    // Compõe a chave versionada a partir do selo de versão vigente. Sem selo (ETL ainda
    // não selou) → null: não há chave determinística, segue direto ao banco sem cachear.
    private async Task<string?> ResolverChaveAsync(ICacheService cache, string cep, CancellationToken cancellationToken)
    {
        string? selo = await ObterSeloVigenteAsync(cache, cancellationToken).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(selo) ? null : $"geo:cep:v{selo}:{cep}";
    }

    // Memoiza o selo em processo (IMemoryCache) por uma janela curta (SeloTtl, #703): o
    // selo só muda em re-selagem do ETL (#674), então no hot path o lookup faz 1
    // round-trip ao Redis (a entrada) em vez de 2 (selo + entrada). A expiração é
    // governada pelo TimeProvider injetado (testável); a expiração do IMemoryCache é a
    // rede de segurança contra leak de memória. Só um selo presente é memoizado — a
    // ausência (bootstrap antes da 1ª selagem) é relida a cada request até selar.
    private async Task<string?> ObterSeloVigenteAsync(ICacheService cache, CancellationToken cancellationToken)
    {
        DateTimeOffset agora = _relogio.GetUtcNow();
        if (_memoria.TryGetValue(ChaveMemoSelo, out SeloMemoizado? memo)
            && memo is not null
            && agora < memo.ExpiraEm)
        {
            return memo.Valor;
        }

        string? selo = await LerSeloDoCacheAsync(cache, cancellationToken).ConfigureAwait(false);

        if (_seloTtl > TimeSpan.Zero && !string.IsNullOrWhiteSpace(selo))
        {
            _memoria.Set(
                ChaveMemoSelo,
                new SeloMemoizado(selo, agora + _seloTtl),
                new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = _seloTtl });
        }

        return selo;
    }

    private async Task<string?> LerSeloDoCacheAsync(ICacheService cache, CancellationToken cancellationToken)
    {
        try
        {
            return await cache
                .ObterAsync<string>(RedisGeoCepCacheInvalidador.ChaveSeloVersaoVigente, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception excecao) when (excecao is not OperationCanceledException)
        {
            LogFalhaLerSelo(_logger, excecao);
            return null;
        }
    }

    // Selo memoizado + instante de expiração (governado pelo TimeProvider).
    private sealed record SeloMemoizado(string Valor, DateTimeOffset ExpiraEm);

    private async Task<CepResolvidoDto?> TentarLerCacheAsync(ICacheService cache, string chave, CancellationToken cancellationToken)
    {
        try
        {
            return await cache.ObterAsync<CepResolvidoDto>(chave, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception excecao) when (excecao is not OperationCanceledException)
        {
            LogFalhaLerCache(_logger, excecao);
            return null;
        }
    }

    private async Task TentarGravarCacheAsync(ICacheService cache, string chave, CepResolvidoDto dto, CancellationToken cancellationToken)
    {
        try
        {
            await cache.DefinirAsync(chave, dto, _ttl, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception excecao) when (excecao is not OperationCanceledException)
        {
            LogFalhaGravarCache(_logger, excecao);
        }
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Geo: cache de CEP indisponível (Redis fora); lookup degrada para o banco.")]
    private static partial void LogCacheIndisponivel(ILogger logger, Exception excecao);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Geo: falha ao ler o selo de versão vigente do cache de CEP; lookup degrada para o banco.")]
    private static partial void LogFalhaLerSelo(ILogger logger, Exception excecao);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Geo: falha ao ler o cache de CEP; lookup degrada para o banco.")]
    private static partial void LogFalhaLerCache(ILogger logger, Exception excecao);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Geo: falha ao gravar o cache de CEP (best-effort); a resolução já foi devolvida.")]
    private static partial void LogFalhaGravarCache(ILogger logger, Exception excecao);
}
