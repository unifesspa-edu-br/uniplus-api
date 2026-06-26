namespace Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Readers;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Geo.Application.Abstractions;
using Unifesspa.UniPlus.Geo.Domain.Entities;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Leitor read-side de <see cref="Bairro"/> sobre o <see cref="GeoDbContext"/>,
/// sempre restrito a uma Cidade vigente e com busca textual opcional.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via DI em GeoInfrastructureRegistration.")]
public sealed class BairroReader : IBairroReader
{
    private readonly GeoDbContext _dbContext;

    public BairroReader(GeoDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public async Task<(bool CidadeExiste, IReadOnlyList<Bairro> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPorCidadeAsync(
        string codigoIbge,
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        string? busca,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigoIbge);

        if (await ResolverCidadeIdAsync(codigoIbge, cancellationToken).ConfigureAwait(false) is not { } cidadeId)
        {
            return (false, [], null, null);
        }

        IQueryable<Bairro> query = _dbContext.Bairros
            .AsNoTracking()
            .Where(b => b.Vigente && b.CidadeId == cidadeId);

        if (!string.IsNullOrWhiteSpace(busca))
        {
            string pattern = BuscaTextualNormalizada.CriarPadraoContem(busca);
            query = query.Where(b =>
                EF.Functions.ILike(b.NomeNormalizado, pattern, BuscaTextualNormalizada.Escape));
        }

        CursorKeysetPage<Bairro> page = await CursorKeyset
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
