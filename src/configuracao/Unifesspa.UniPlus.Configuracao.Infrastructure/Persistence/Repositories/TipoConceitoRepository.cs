namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Pagination;

public sealed class TipoConceitoRepository : ITipoConceitoRepository
{
    private readonly UniPlusDbContext _dbContext;

    public TipoConceitoRepository(UniPlusDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public Task<TipoConceito?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken) =>
        _dbContext.TiposConceito.FirstOrDefaultAsync(tipo => tipo.Id == id, cancellationToken);

    public Task<TipoConceito?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken) =>
        _dbContext.TiposConceito.AsNoTracking().FirstOrDefaultAsync(tipo => tipo.Id == id, cancellationToken);

    public async Task<(IReadOnlyList<TipoConceito> Itens, (string SortKey, Guid Id)? Anterior, (string SortKey, Guid Id)? Proximo)> ListarPaginadoAsync(
        string? afterSortKey,
        Guid? afterId, int limit, PaginationDirection direction, CancellationToken cancellationToken)
    {
        OrderedKeysetPage<TipoConceito> page = await CursorKeyset
            .ApplyAsync(_dbContext.TiposConceito.AsNoTracking().Where(tipo => tipo.Ativo), CondicoesDoFiltro.Nenhuma, afterSortKey, afterId, limit, direction, cancellationToken)
            .ConfigureAwait(false);
        return (page.Items, page.Previous, page.Next);
    }

    public async Task AdicionarAsync(TipoConceito tipo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tipo);
        await _dbContext.TiposConceito.AddAsync(tipo, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TipoConceito>> ListarPorCodigosAsync(
        IReadOnlyCollection<string> codigos, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(codigos);
        return await _dbContext.TiposConceito.Where(tipo => codigos.Contains(tipo.Codigo))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<bool> CodigoExisteAsync(string codigo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(codigo);
        return _dbContext.TiposConceito.AsNoTracking().AnyAsync(tipo => tipo.Codigo == codigo, cancellationToken);
    }
}
