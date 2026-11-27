namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Pagination;

public sealed class TipoEtapaRepository : ITipoEtapaRepository
{
    private readonly UniPlusDbContext _dbContext;

    public TipoEtapaRepository(UniPlusDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public Task<TipoEtapa?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken) =>
        _dbContext.TiposEtapa.Include(tipo => tipo.ConceitosAdmitidos).FirstOrDefaultAsync(tipo => tipo.Id == id, cancellationToken);

    public Task<TipoEtapa?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken) =>
        _dbContext.TiposEtapa.AsNoTracking().Include(tipo => tipo.ConceitosAdmitidos)
            .FirstOrDefaultAsync(tipo => tipo.Id == id, cancellationToken);

    public async Task<(IReadOnlyList<TipoEtapa> Itens, (string SortKey, Guid Id)? Anterior, (string SortKey, Guid Id)? Proximo)> ListarPaginadoAsync(
        string? afterSortKey,
        Guid? afterId, int limit, PaginationDirection direction, CancellationToken cancellationToken)
    {
        OrderedKeysetPage<TipoEtapa> page = await CursorKeyset
            .ApplyAsync(
                _dbContext.TiposEtapa.AsNoTracking().Include(tipo => tipo.ConceitosAdmitidos).Where(tipo => tipo.Ativo), CondicoesDoFiltro.Nenhuma, afterSortKey, afterId, limit, direction, cancellationToken)
            .ConfigureAwait(false);
        return (page.Items, page.Previous, page.Next);
    }

    public async Task AdicionarAsync(TipoEtapa tipo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tipo);
        await _dbContext.TiposEtapa.AddAsync(tipo, cancellationToken).ConfigureAwait(false);
    }

    public Task<bool> CodigoExisteAsync(string codigo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(codigo);
        return _dbContext.TiposEtapa.AsNoTracking().AnyAsync(tipo => tipo.Codigo == codigo, cancellationToken);
    }
}
