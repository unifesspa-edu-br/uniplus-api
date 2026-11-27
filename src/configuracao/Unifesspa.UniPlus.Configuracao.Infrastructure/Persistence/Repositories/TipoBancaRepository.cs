namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Regras.Enums;

public sealed class TipoBancaRepository : ITipoBancaRepository
{
    private readonly UniPlusDbContext _dbContext;

    public TipoBancaRepository(UniPlusDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public Task<TipoBanca?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.TiposBanca
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    public Task<TipoBanca?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.TiposBanca
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    public async Task<(IReadOnlyList<TipoBanca> Itens, (string SortKey, Guid Id)? Anterior, (string SortKey, Guid Id)? Proximo)> ListarPaginadoAsync(
        string? afterSortKey,
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken)
    {
        // Keyset bidirecional (ADR-0089): ordenação por Id (Guid v7, ADR-0026/0032).
        OrderedKeysetPage<TipoBanca> page = await CursorKeyset
            .ApplyAsync(_dbContext.TiposBanca.AsNoTracking(), CondicoesDoFiltro.Nenhuma, afterSortKey, afterId, limit, direction, cancellationToken)
            .ConfigureAwait(false);

        return (page.Items, page.Previous, page.Next);
    }

    public async Task AdicionarAsync(TipoBanca banca, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(banca);
        await _dbContext.TiposBanca.AddAsync(banca, cancellationToken).ConfigureAwait(false);
    }

    public void Remover(TipoBanca banca)
    {
        ArgumentNullException.ThrowIfNull(banca);
        _dbContext.TiposBanca.Remove(banca);
    }

    public Task<bool> CodigoExisteEntreVivosAsync(
        TipoDeBanca codigo,
        Guid? excluirId,
        CancellationToken cancellationToken)
    {
        return _dbContext.TiposBanca
            .AsNoTracking()
            .Where(b => excluirId == null || b.Id != excluirId)
            .AnyAsync(b => b.Codigo == codigo, cancellationToken);
    }
}
