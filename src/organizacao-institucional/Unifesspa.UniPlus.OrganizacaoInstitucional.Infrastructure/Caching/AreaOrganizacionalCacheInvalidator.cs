namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Caching;

using Microsoft.Extensions.Logging;

using Unifesspa.UniPlus.Infrastructure.Core.Caching;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Abstractions;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via DI em OrganizacaoInstitucionalInfrastructureRegistration.AddOrganizacaoInstitucionalInfrastructure.")]
internal sealed partial class AreaOrganizacionalCacheInvalidator : IAreaOrganizacionalCacheInvalidator
{
    private readonly ICacheService _cache;
    private readonly ILogger<AreaOrganizacionalCacheInvalidator> _logger;

    public AreaOrganizacionalCacheInvalidator(
        ICacheService cache,
        ILogger<AreaOrganizacionalCacheInvalidator> logger)
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
            await _cache.RemoverAsync(AreaOrganizacionalCacheKeys.TodasAsAreasAtivas, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Best-effort: cache fica stale até TTL natural. Próxima leitura
            // repovoa a partir do banco — auditoria ainda preserva o estado.
            LogFalhaInvalidacao(_logger, ex);
        }
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Falha ao invalidar cache de areas-organizacionais ativas após mutação — cache stale até TTL natural (5 min).")]
    private static partial void LogFalhaInvalidacao(ILogger logger, Exception exception);
}
