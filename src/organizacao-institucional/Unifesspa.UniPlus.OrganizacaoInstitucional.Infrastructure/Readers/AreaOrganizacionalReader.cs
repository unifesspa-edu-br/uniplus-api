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
/// Implementação canônica de <see cref="IAreaOrganizacionalReader"/>:
/// cache Redis (TTL 5 min) + stampede protection via lease ~300 ms
/// (ADR-0057 Pattern 4). Filtro por código é em memória após o cache hit —
/// catálogo é bounded (≤ 20 entradas).
/// </summary>
/// <remarks>
/// <para>Lifetime <strong>Scoped</strong>: depende de <see cref="ICacheService"/>
/// (Scoped) e <see cref="OrganizacaoInstitucionalDbContext"/> (Scoped). Não há
/// estado interno; instância por request é o suficiente — pressão de GC para
/// catálogo dessa cardinalidade é negligível.</para>
/// <para>Hook de tempo de fonte (<see cref="DelayAntesDoSourceParaTeste"/>):
/// usado <strong>apenas</strong> pelo integration test de stampede protection
/// para garantir contenção real entre callers concorrentes. Em produção fica
/// em <see cref="TimeSpan.Zero"/>.</para>
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via DI em OrganizacaoInstitucionalInfrastructureRegistration.AddOrganizacaoInstitucionalInfrastructure.")]
internal sealed partial class AreaOrganizacionalReader : IAreaOrganizacionalReader
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    // LeaseTtl = 2s cobre source latency típica do catálogo (≤20 entries; ~10-100ms
    // warm, até ~500ms cold) com folga. Maior que isso reduz throughput se o
    // populador crashar — TTL natural retoma após esse intervalo.
    private static readonly TimeSpan LeaseTtl = TimeSpan.FromSeconds(2);

    // 25 × 100ms = 2.5s espera dos "losers" do lease antes de cair à fonte direta.
    // Garante BDD-2 (100 concorrentes → 1 query) para source ≤2.5s; acima disso
    // degrada para múltiplas queries (fail-open documentado).
    private static readonly TimeSpan EsperaCurta = TimeSpan.FromMilliseconds(100);
    private const int TentativasDeRecheck = 25;

    // Hook test-only: integration test ajusta para forçar contenção real do
    // lease quando todos os callers paralelos atingem o source. Default zero
    // — produção não sofre overhead.
    internal static TimeSpan DelayAntesDoSourceParaTeste { get; set; } = TimeSpan.Zero;

    private readonly ICacheService _cache;
    private readonly OrganizacaoInstitucionalDbContext _dbContext;
    private readonly ILogger<AreaOrganizacionalReader> _logger;

    public AreaOrganizacionalReader(
        ICacheService cache,
        OrganizacaoInstitucionalDbContext dbContext,
        ILogger<AreaOrganizacionalReader> logger)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(logger);
        _cache = cache;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AreaOrganizacionalView>> ListarAtivasAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Cache hit é o caminho mais quente — 1 round-trip Redis warm.
        List<AreaOrganizacionalView>? cacheado = await ObterDoCacheAsync(cancellationToken)
            .ConfigureAwait(false);
        if (cacheado is not null)
        {
            return cacheado;
        }

        // Cache miss — tenta adquirir o lease (stampede protection). try/finally
        // explícito em vez de `await using` para satisfazer CA2007 com IAsyncDisposable
        // nullable retornado pelo lease.
        IAsyncDisposable? lease = await _cache
            .AcquireLeaseAsync(AreaOrganizacionalCacheKeys.LeaseTodasAsAreasAtivas, LeaseTtl, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            if (lease is null)
            {
                // Lease perdido — outro caller está populando. Espera curta + recheck.
                // Após N tentativas sem sucesso, vai à fonte direta (fail-open): preferimos
                // pagar uma query a mais a degradar latência além de ~500 ms.
                for (int tentativa = 0; tentativa < TentativasDeRecheck; tentativa++)
                {
                    await Task.Delay(EsperaCurta, cancellationToken).ConfigureAwait(false);
                    cacheado = await ObterDoCacheAsync(cancellationToken).ConfigureAwait(false);
                    if (cacheado is not null)
                    {
                        return cacheado;
                    }
                }

                LogLeaseEsgotouSemPopular(_logger, AreaOrganizacionalCacheKeys.TodasAsAreasAtivas);
                // Fall-through — vai à fonte sem lease (degradação aceita).
            }
            else
            {
                // Re-leitura defensiva após adquirir o lease — outro caller pode ter populado
                // entre o miss inicial e a aquisição do lease (race janela ~µs).
                cacheado = await ObterDoCacheAsync(cancellationToken).ConfigureAwait(false);
                if (cacheado is not null)
                {
                    return cacheado;
                }
            }

            // Caminho frio — única query ao Postgres por janela de cache miss.
            IReadOnlyList<AreaOrganizacionalView> views = await CarregarDoSourceAsync(cancellationToken)
                .ConfigureAwait(false);

            try
            {
                await _cache.DefinirAsync(
                    AreaOrganizacionalCacheKeys.TodasAsAreasAtivas,
                    views,
                    CacheTtl,
                    cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Falha de escrita no cache não interrompe o request. Próxima
                // chamada repete o carregamento até o cache responder.
                LogFalhaPopularCache(_logger, ex);
            }

            return views;
        }
        finally
        {
            if (lease is not null)
            {
                await lease.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    public async Task<AreaOrganizacionalView?> ObterPorCodigoAsync(
        AreaCodigo codigo,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<AreaOrganizacionalView> todas = await ListarAtivasAsync(cancellationToken)
            .ConfigureAwait(false);
        // O(N) sobre coleção bounded (≤ ~20 entradas) — sem custo real vs index.
        return todas.FirstOrDefault(a => a.Codigo == codigo);
    }

    private async Task<List<AreaOrganizacionalView>?> ObterDoCacheAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _cache.ObterAsync<List<AreaOrganizacionalView>>(
                AreaOrganizacionalCacheKeys.TodasAsAreasAtivas,
                cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Redis temporariamente indisponível: cai para fonte direta na
            // chamada externa (fail-open). Auditoria ainda preserva o estado.
            LogFalhaLerCache(_logger, ex);
            return null;
        }
    }

    private async Task<IReadOnlyList<AreaOrganizacionalView>> CarregarDoSourceAsync(CancellationToken cancellationToken)
    {
        if (DelayAntesDoSourceParaTeste > TimeSpan.Zero)
        {
            await Task.Delay(DelayAntesDoSourceParaTeste, cancellationToken).ConfigureAwait(false);
        }

        List<AreaOrganizacional> entidades = await _dbContext.AreasOrganizacionais
            .AsNoTracking()
            .OrderBy(a => a.Codigo)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. entidades.Select(a => a.ToView())];
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Lease para chave {ChaveLease} esgotou sem cache popular — caindo à fonte direta (fail-open).")]
    private static partial void LogLeaseEsgotouSemPopular(ILogger logger, string chaveLease);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Falha ao popular cache de areas-organizacionais após carregar da fonte.")]
    private static partial void LogFalhaPopularCache(ILogger logger, Exception exception);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Falha ao ler cache de areas-organizacionais; usando fonte direta.")]
    private static partial void LogFalhaLerCache(ILogger logger, Exception exception);
}
