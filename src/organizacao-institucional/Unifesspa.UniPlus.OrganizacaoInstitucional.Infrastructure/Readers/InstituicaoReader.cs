namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Readers;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Infrastructure.Core.Caching;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Mappings;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Caching;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence;

/// <summary>
/// Implementação canônica de <see cref="IInstituicaoReader"/>: cache Redis
/// (TTL 5 min) fail-open sobre a <c>Instituicao</c> singleton.
/// </summary>
/// <remarks>
/// Sem stampede protection por lease (ao contrário do <c>UnidadeReader</c>): a
/// carga da fonte é uma consulta de uma única linha (singleton — ADR-0055),
/// barata o bastante para que uma corrida de cache miss não justifique a
/// coordenação. Nunca cacheia ausência — quando ainda não há Instituição
/// cadastrada, a fonte é consultada a cada chamada até a primeira criação.
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via DI em OrganizacaoInstitucionalInfrastructureRegistration.")]
internal sealed partial class InstituicaoReader : IInstituicaoReader
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private readonly ICacheService _cache;
    private readonly OrganizacaoInstitucionalDbContext _dbContext;
    private readonly ILogger<InstituicaoReader> _logger;

    public InstituicaoReader(
        ICacheService cache,
        OrganizacaoInstitucionalDbContext dbContext,
        ILogger<InstituicaoReader> logger)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(logger);
        _cache = cache;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<InstituicaoView?> ObterAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        InstituicaoView? cacheado = await ObterDoCacheAsync(cancellationToken).ConfigureAwait(false);
        if (cacheado is not null)
        {
            return cacheado;
        }

        Instituicao? instituicao = await _dbContext.Instituicoes
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (instituicao is null)
        {
            return null;
        }

        InstituicaoView view = instituicao.ToView();

        try
        {
            await _cache.DefinirAsync(InstituicaoCacheKeys.InstituicaoAtual, view, CacheTtl, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogFalhaPopularCache(_logger, ex);
        }

        return view;
    }

    private async Task<InstituicaoView?> ObterDoCacheAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _cache.ObterAsync<InstituicaoView>(
                InstituicaoCacheKeys.InstituicaoAtual,
                cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogFalhaLerCache(_logger, ex);
            return null;
        }
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Falha ao popular cache da Instituição após carregar da fonte.")]
    private static partial void LogFalhaPopularCache(ILogger logger, Exception exception);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Falha ao ler cache da Instituição; usando fonte direta.")]
    private static partial void LogFalhaLerCache(ILogger logger, Exception exception);
}
