namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Pagination;

public sealed class BaseLegalDoFadaRepository : IBaseLegalDoFadaRepository
{
    private readonly UniPlusDbContext _dbContext;

    public BaseLegalDoFadaRepository(UniPlusDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public Task<BaseLegalDoFada?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.BasesLegaisDoFada
            .Include(x => x.Municipios)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public Task<bool> CodigoExisteAsync(CodigoDaBaseLegalDoFada codigo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(codigo);
        return _dbContext.BasesLegaisDoFada.AsNoTracking().AnyAsync(x => x.Codigo == codigo, cancellationToken);
    }

    public async Task<IReadOnlyList<BaseLegalDoFada>> ListarVivasParaLeituraAsync(CancellationToken cancellationToken) =>
        await _dbContext.BasesLegaisDoFada.AsNoTracking().Include(x => x.Municipios).ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task<BaseLegalDoFada?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.BasesLegaisDoFada
            .AsNoTracking()
            .Include(x => x.Municipios)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<(IReadOnlyList<BaseLegalDoFada> Itens, (string SortKey, Guid Id)? Anterior, (string SortKey, Guid Id)? Proximo)> ListarPaginadoAsync(
        string? afterSortKey,
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken)
    {
        OrderedKeysetPage<BaseLegalDoFada> page = await CursorKeyset
            .ApplyAsync(_dbContext.BasesLegaisDoFada.AsNoTracking().Include(x => x.Municipios), CondicoesDoFiltro.Nenhuma, afterSortKey, afterId, limit, direction, cancellationToken)
            .ConfigureAwait(false);

        return (page.Items, page.Previous, page.Next);
    }

    public async Task AdicionarAsync(BaseLegalDoFada entity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entity);
        await _dbContext.BasesLegaisDoFada.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public void Remover(BaseLegalDoFada entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        _dbContext.BasesLegaisDoFada.Remove(entity);
    }
}
