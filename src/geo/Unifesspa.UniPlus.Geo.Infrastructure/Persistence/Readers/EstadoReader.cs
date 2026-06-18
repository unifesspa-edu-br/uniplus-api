namespace Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Readers;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Geo.Application.Abstractions;
using Unifesspa.UniPlus.Geo.Domain.Entities;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Leitor read-side de <see cref="Estado"/> sobre o <see cref="GeoDbContext"/>.
/// Só expõe reference data vigente (<c>vigente=true</c>, ADR-0092) — linhas stale
/// do ETL não vazam na API pública. Listagem por keyset bidirecional
/// (<see cref="CursorKeyset"/>); os <c>EXISTS</c> do keyset herdam o filtro de
/// vigência por operarem sobre a mesma query base.
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

    public async Task<(IReadOnlyList<Estado> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken)
    {
        IQueryable<Estado> query = _dbContext.Estados
            .AsNoTracking()
            .Where(e => e.Vigente);

        CursorKeysetPage<Estado> page = await CursorKeyset
            .ApplyAsync(query, afterId, limit, direction, cancellationToken)
            .ConfigureAwait(false);

        return (page.Items, page.PrevAfterId, page.NextAfterId);
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
