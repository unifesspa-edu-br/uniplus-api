namespace Unifesspa.UniPlus.Geo.Infrastructure.Caching;

using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Logging;

using Unifesspa.UniPlus.Geo.Application.Abstractions;
using Unifesspa.UniPlus.Infrastructure.Core.Caching;

/// <summary>
/// Invalida o cache local de lookup de CEP por <strong>selo de versão</strong> (Story
/// #674, CA-05): grava a versão vigente numa única chave (<see cref="ChaveSeloVersaoVigente"/>).
/// O lookup de CEP (F4, #676) compõe a chave de cache com esse selo; trocar o selo torna
/// todas as entradas de versões anteriores um <em>miss</em> (recomputadas do banco já na
/// nova versão), que expiram por TTL. É O(1) e não varre o keyspace (<c>SCAN</c>/<c>KEYS</c>
/// são proibidos em produção — o Redis é compartilhado entre módulos) nem toca cache de
/// outro módulo.
/// </summary>
internal sealed partial class RedisGeoCepCacheInvalidador : IGeoCepCacheInvalidador
{
    /// <summary>Chave do selo de versão vigente do lookup de CEP. O reader (F4) compõe suas chaves com este selo.</summary>
    public const string ChaveSeloVersaoVigente = "geo:cep:versao-vigente";

    private readonly ICacheService _cache;
    private readonly ILogger<RedisGeoCepCacheInvalidador> _logger;

    public RedisGeoCepCacheInvalidador(ICacheService cache, ILogger<RedisGeoCepCacheInvalidador> logger)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(logger);
        _cache = cache;
        _logger = logger;
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Invalidação best-effort: a carga já concluiu; qualquer falha de cache (Redis fora, timeout) degrada o lookup para leitura do banco, não pode falhar a carga.")]
    public async Task InvalidarAsync(string versaoVigente, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(versaoVigente);

        try
        {
            await _cache.DefinirAsync(ChaveSeloVersaoVigente, versaoVigente, expiracao: null, cancellationToken).ConfigureAwait(false);
            LogSeloAtualizado(_logger, versaoVigente);
        }
        catch (Exception excecao) when (excecao is not OperationCanceledException)
        {
            LogFalhaSelo(_logger, versaoVigente, excecao);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Geo: selo de versão vigente do cache de CEP atualizado para {Versao}.")]
    private static partial void LogSeloAtualizado(ILogger logger, string versao);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Geo: falha ao selar versão vigente {Versao} no cache de CEP (best-effort; lookup degrada para o banco).")]
    private static partial void LogFalhaSelo(ILogger logger, string versao, Exception excecao);
}
