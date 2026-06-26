namespace Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Readers;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Geo.Application.Abstractions;
using Unifesspa.UniPlus.Geo.Domain.Entities;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Leitor read-side de <see cref="Distrito"/> sobre o <see cref="GeoDbContext"/>,
/// sempre restrito a uma Cidade vigente.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via DI em GeoInfrastructureRegistration.")]
public sealed class DistritoReader : IDistritoReader
{
    private readonly GeoDbContext _dbContext;

    public DistritoReader(GeoDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public async Task<(bool CidadeExiste, IReadOnlyList<Distrito> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPorCidadeAsync(
        string codigoIbge,
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigoIbge);

        if (await ResolverCidadeIdAsync(codigoIbge, cancellationToken).ConfigureAwait(false) is not { } cidadeId)
        {
            return (false, [], null, null);
        }

        IQueryable<Distrito> query = _dbContext.Distritos
            .AsNoTracking()
            .Where(d => d.Vigente && d.CidadeId == cidadeId);

        CursorKeysetPage<Distrito> page = await CursorKeyset
            .ApplyAsync(query, afterId, limit, direction, cancellationToken)
            .ConfigureAwait(false);

        return (true, page.Items, page.PrevAfterId, page.NextAfterId);
    }

    private Task<Guid?> ResolverCidadeIdAsync(string codigoIbge, CancellationToken cancellationToken)
    {
        string codigo = codigoIbge.Trim();
        return _dbContext.Cidades
            .AsNoTracking()
            .Where(c => c.Vigente && c.CodigoIbge == codigo)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
