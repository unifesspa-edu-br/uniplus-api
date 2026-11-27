namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Readers;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Infrastructure.Core.Caching;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Mappings;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Caching;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence;

/// <summary>
/// Implementação canônica de <see cref="IUnidadeReader"/>:
/// cache Redis (TTL 5 min) + stampede protection via lease ~2s
/// (ADR-0057 Pattern 4).
/// </summary>
/// <remarks>
/// Pública (não <c>internal</c>) porque é injetada em handlers Wolverine de
/// outros módulos co-hospedados (ex.: o congelamento da unidade ofertante na
/// <c>OfertaCurso</c> de Configuração) e, sob <c>ServiceLocationPolicy.NotAllowed</c>
/// (ADR-0098), o codegen precisa construir o tipo concreto — mesmo root fix
/// aplicado aos cache invalidators.
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via DI em OrganizacaoInstitucionalInfrastructureRegistration.")]
public sealed partial class UnidadeReader : IUnidadeReader
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan LeaseTtl = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan EsperaCurta = TimeSpan.FromMilliseconds(100);
    private const int TentativasDeRecheck = 25;

    internal static TimeSpan DelayAntesDoSourceParaTeste { get; set; } = TimeSpan.Zero;

    private readonly ICacheService _cache;
    private readonly UniPlusDbContext _dbContext;
    private readonly ILogger<UnidadeReader> _logger;

    public UnidadeReader(
        ICacheService cache,
        UniPlusDbContext dbContext,
        ILogger<UnidadeReader> logger)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(logger);
        _cache = cache;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<IReadOnlyList<UnidadeView>> ListarAtivasAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        List<UnidadeView>? cacheado = await ObterDoCacheAsync(cancellationToken).ConfigureAwait(false);
        return cacheado ?? await CarregarSobLeaseAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<UnidadeView>> CarregarSobLeaseAsync(CancellationToken cancellationToken)
    {
        (IAsyncDisposable? lease, bool leaseIndisponivelPorOutage) = await TentarAdquirirLeaseAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await ObterDoCacheAposLeaseAsync(lease, leaseIndisponivelPorOutage, cancellationToken).ConfigureAwait(false)
                ?? await CarregarDoSourceEPopularCacheAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (lease is not null)
            {
                await lease.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private async Task<(IAsyncDisposable? Lease, bool IndisponivelPorOutage)> TentarAdquirirLeaseAsync(CancellationToken cancellationToken)
    {
        try
        {
            IAsyncDisposable? lease = await _cache
                .AcquireLeaseAsync(UnidadeCacheKeys.LeaseTodasAsUnidadesAtivas, LeaseTtl, cancellationToken)
                .ConfigureAwait(false);
            return (lease, false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogFalhaAdquirirLease(_logger, ex);
            return (null, true);
        }
    }

    private async Task<List<UnidadeView>?> ObterDoCacheAposLeaseAsync(IAsyncDisposable? lease, bool leaseIndisponivelPorOutage, CancellationToken cancellationToken)
    {
        if (leaseIndisponivelPorOutage)
        {
            return null;
        }

        return lease is not null
            ? await ObterDoCacheAsync(cancellationToken).ConfigureAwait(false)
            : await EsperarOCachePopuladoAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<List<UnidadeView>?> EsperarOCachePopuladoAsync(CancellationToken cancellationToken)
    {
        for (int tentativa = 0; tentativa < TentativasDeRecheck; tentativa++)
        {
            await Task.Delay(EsperaCurta, cancellationToken).ConfigureAwait(false);
            List<UnidadeView>? cacheado = await ObterDoCacheAsync(cancellationToken).ConfigureAwait(false);
            if (cacheado is not null)
            {
                return cacheado;
            }
        }

        LogLeaseEsgotouSemPopular(_logger, UnidadeCacheKeys.TodasAsUnidadesAtivas);
        return null;
    }

    private async Task<IReadOnlyList<UnidadeView>> CarregarDoSourceEPopularCacheAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<UnidadeView> views = await CarregarDoSourceAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _cache.DefinirAsync(UnidadeCacheKeys.TodasAsUnidadesAtivas, views, CacheTtl, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogFalhaPopularCache(_logger, ex);
        }

        return views;
    }

    public async Task<UnidadeView?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<UnidadeView> todas = await ListarAtivasAsync(cancellationToken).ConfigureAwait(false);
        return todas.FirstOrDefault(u => u.Id == id);
    }

    public async Task<UnidadeView?> ObterPorCodigoAsync(string codigo, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigo);

        Unidade? unidade = await _dbContext.Unidades
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Codigo == codigo, cancellationToken)
            .ConfigureAwait(false);

        return unidade?.ToView();
    }

    public async Task<IReadOnlyList<UnidadeView>> ListarPorCodigosAsync(
        IReadOnlyCollection<string> codigos,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(codigos);

        // O caractere nulo não cabe em coluna de texto do Postgres e faria o parâmetro ser recusado: esse código não tem Unidade.
        List<string> codigosDoLote = [.. codigos.Where(static codigo => !codigo.Contains('\0', StringComparison.Ordinal)).Distinct(StringComparer.Ordinal)];
        List<Unidade> unidades = await _dbContext.Unidades
            .AsNoTracking()
            .Where(u => codigosDoLote.Contains(u.Codigo))
            .OrderBy(u => u.Sigla)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. unidades.Select(u => u.ToView())];
    }

    private async Task<List<UnidadeView>?> ObterDoCacheAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _cache.ObterAsync<List<UnidadeView>>(
                UnidadeCacheKeys.TodasAsUnidadesAtivas,
                cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogFalhaLerCache(_logger, ex);
            return null;
        }
    }

    private async Task<IReadOnlyList<UnidadeView>> CarregarDoSourceAsync(CancellationToken cancellationToken)
    {
        if (DelayAntesDoSourceParaTeste > TimeSpan.Zero)
        {
            await Task.Delay(DelayAntesDoSourceParaTeste, cancellationToken).ConfigureAwait(false);
        }

        List<Unidade> entidades = await _dbContext.Unidades
            .AsNoTracking()
            .OrderBy(u => u.Sigla)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. entidades.Select(u => u.ToView())];
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Lease para chave {ChaveLease} esgotou sem cache popular — caindo à fonte direta (fail-open).")]
    private static partial void LogLeaseEsgotouSemPopular(ILogger logger, string chaveLease);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Falha ao popular cache de unidades ativas após carregar da fonte.")]
    private static partial void LogFalhaPopularCache(ILogger logger, Exception exception);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Falha ao ler cache de unidades ativas; usando fonte direta.")]
    private static partial void LogFalhaLerCache(ILogger logger, Exception exception);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Falha ao adquirir lease de stampede protection para unidades; seguindo sem coordenação (cache fail-open).")]
    private static partial void LogFalhaAdquirirLease(ILogger logger, Exception exception);
}
