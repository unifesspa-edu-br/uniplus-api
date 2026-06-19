namespace Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Readers;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;

using MR.EntityFrameworkCore.KeysetPagination;

using Unifesspa.UniPlus.Geo.Application.Abstractions;
using Unifesspa.UniPlus.Geo.Domain.Entities;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Leitor read-side de <see cref="Estado"/> sobre o <see cref="GeoDbContext"/>.
/// Só expõe reference data vigente (<c>vigente=true</c>, ADR-0092) — linhas stale
/// do ETL não vazam na API pública. Listagem por keyset bidirecional ordenado
/// alfabeticamente por nome (<see cref="KeysetOrdenadoCursor"/>, ADR-0094); os
/// <c>EXISTS</c> do keyset herdam o filtro de vigência por operarem sobre a mesma
/// query base.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via DI em GeoInfrastructureRegistration.")]
internal sealed class EstadoReader : IEstadoReader
{
    private readonly GeoDbContext _dbContext;

    public EstadoReader(GeoDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public async Task<(IReadOnlyList<Estado> Itens, (string SortKey, Guid Id)? Anterior, (string SortKey, Guid Id)? Proximo)> ListarPaginadoAsync(
        string? afterSortKey,
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken)
    {
        IQueryable<Estado> query = _dbContext.Estados
            .AsNoTracking()
            .Where(e => e.Vigente);

        // Keyset ordenado por nome (coalesce não-nulo, ADR-0095) + Id de desempate.
        KeysetOrdenadoPage<Estado> page = await KeysetOrdenadoCursor
            .ApplyAsync(
                query,
                b => b.Ascending(e => e.NomeNormalizado ?? string.Empty).Ascending(e => e.Id),
                e => e.NomeNormalizado ?? string.Empty,
                static (sortKey, id) => new { NomeNormalizado = sortKey, Id = id },
                afterSortKey,
                afterId,
                limit,
                direction,
                cancellationToken)
            .ConfigureAwait(false);

        return (page.Items, page.Anterior, page.Proximo);
    }

    public Task<Estado?> ObterPorUfAsync(string uf, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(uf);

        // Chave natural normalizada para maiúsculas (espelha Estado.Importar).
        string ufNormalizada = uf.Trim().ToUpperInvariant();

        return _dbContext.Estados
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Vigente && e.Uf == ufNormalizada, cancellationToken);
    }
}
