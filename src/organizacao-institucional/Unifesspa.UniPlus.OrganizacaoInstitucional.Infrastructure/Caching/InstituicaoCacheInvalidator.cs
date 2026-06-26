namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Caching;

using Microsoft.Extensions.Logging;

using Unifesspa.UniPlus.Infrastructure.Core.Caching;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Abstractions;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via DI em OrganizacaoInstitucionalInfrastructureRegistration.")]
public sealed partial class InstituicaoCacheInvalidator : IInstituicaoCacheInvalidator
{
    private readonly ICacheService _cache;
    private readonly ILogger<InstituicaoCacheInvalidator> _logger;

    public InstituicaoCacheInvalidator(ICacheService cache, ILogger<InstituicaoCacheInvalidator> logger)
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
            await _cache.RemoverAsync(InstituicaoCacheKeys.InstituicaoAtual, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogFalhaInvalidacao(_logger, ex);
        }
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Falha ao invalidar cache da Instituição após mutação — cache stale até TTL natural.")]
    private static partial void LogFalhaInvalidacao(ILogger logger, Exception exception);
}
