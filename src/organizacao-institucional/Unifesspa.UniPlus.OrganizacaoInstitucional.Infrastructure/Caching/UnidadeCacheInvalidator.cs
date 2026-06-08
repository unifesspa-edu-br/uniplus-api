namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Caching;

using Microsoft.Extensions.Logging;

using Unifesspa.UniPlus.Infrastructure.Core.Caching;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Abstractions;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via DI em OrganizacaoInstitucionalInfrastructureRegistration.")]
internal sealed partial class UnidadeCacheInvalidator : IUnidadeCacheInvalidator
{
    private readonly ICacheService _cache;
    private readonly ILogger<UnidadeCacheInvalidator> _logger;

    public UnidadeCacheInvalidator(ICacheService cache, ILogger<UnidadeCacheInvalidator> logger)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(logger);
        _cache = cache;
        _logger = logger;
    }

    public async Task InvalidarAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _cache.RemoverAsync(UnidadeCacheKeys.TodasAsUnidadesAtivas, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogFalhaInvalidacao(_logger, ex);
        }
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Falha ao invalidar cache de unidades ativas após mutação — cache stale até TTL natural (5 min).")]
    private static partial void LogFalhaInvalidacao(ILogger logger, Exception exception);
}
