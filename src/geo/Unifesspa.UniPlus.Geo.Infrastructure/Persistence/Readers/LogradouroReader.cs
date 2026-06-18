namespace Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Readers;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Geo.Application.Abstractions;
using Unifesspa.UniPlus.Geo.Domain.Entities;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Leitor read-side de <see cref="Logradouro"/> sobre o <see cref="GeoDbContext"/>,
/// sempre restrito a uma Cidade vigente e com busca textual opcional.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via DI em GeoInfrastructureRegistration.")]
internal sealed class LogradouroReader : ILogradouroReader
{
    private readonly GeoDbContext _dbContext;

    public LogradouroReader(GeoDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public async Task<(bool CidadeExiste, IReadOnlyList<LogradouroComBairro> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPorCidadeAsync(
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

        IQueryable<Logradouro> query = _dbContext.Logradouros
            .AsNoTracking()
            .Where(l => l.Vigente && l.CidadeId == cidadeId);

        if (!string.IsNullOrWhiteSpace(busca))
        {
            string pattern = BuscaTextualNormalizada.CriarPadraoContem(busca);
            query = query.Where(l =>
                EF.Functions.ILike(l.NomeNormalizado, pattern, BuscaTextualNormalizada.Escape));
        }

        CursorKeysetPage<Logradouro> page = await CursorKeyset
            .ApplyAsync(query, afterId, limit, direction, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<LogradouroComBairro> itens = await EnriquecerComBairroAsync(page.Items, cancellationToken)
            .ConfigureAwait(false);

        return (true, itens, page.PrevAfterId, page.NextAfterId);
    }

    private async Task<IReadOnlyList<LogradouroComBairro>> EnriquecerComBairroAsync(
        IReadOnlyList<Logradouro> logradouros,
        CancellationToken cancellationToken)
    {
        if (logradouros.Count == 0)
        {
            return [];
        }

        Guid[] bairroIds = [.. logradouros
            .Select(l => l.BairroId)
            .OfType<Guid>()
            .Distinct()];

        Dictionary<Guid, string> bairros = bairroIds.Length == 0
            ? []
            : await _dbContext.Bairros
                .AsNoTracking()
                .Where(b => b.Vigente && bairroIds.Contains(b.Id))
                .ToDictionaryAsync(b => b.Id, b => b.Nome, cancellationToken)
                .ConfigureAwait(false);

        return [.. logradouros.Select(l =>
            new LogradouroComBairro(
                l,
                l.BairroId is { } bairroId && bairros.TryGetValue(bairroId, out string? nome) ? nome : null))];
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
