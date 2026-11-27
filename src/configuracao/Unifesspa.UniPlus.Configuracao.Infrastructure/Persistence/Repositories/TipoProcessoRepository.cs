namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Pagination;

public sealed class TipoProcessoRepository : ITipoProcessoRepository
{
    private readonly UniPlusDbContext _dbContext;

    public TipoProcessoRepository(UniPlusDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public Task<TipoProcesso?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken) =>
        _dbContext.TiposProcesso.FirstOrDefaultAsync(tipo => tipo.Id == id, cancellationToken);

    public Task<TipoProcesso?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken) =>
        _dbContext.TiposProcesso.AsNoTracking().FirstOrDefaultAsync(tipo => tipo.Id == id, cancellationToken);

    public async Task<(IReadOnlyList<TipoProcesso> Itens, (string SortKey, Guid Id)? Anterior, (string SortKey, Guid Id)? Proximo)> ListarPaginadoAsync(
        string? afterSortKey,
        Guid? afterId, int limit, PaginationDirection direction, bool apenasAtivos, CancellationToken cancellationToken)
    {
        IQueryable<TipoProcesso> consulta = _dbContext.TiposProcesso.AsNoTracking();
        if (apenasAtivos)
        {
            consulta = consulta.Where(tipo => tipo.Ativo);
        }

        OrderedKeysetPage<TipoProcesso> page = await CursorKeyset
            .ApplyAsync(consulta, CondicoesDoFiltro.De(ParteDoFiltro.De(apenasAtivos)), afterSortKey, afterId, limit, direction, cancellationToken)
            .ConfigureAwait(false);
        return (page.Items, page.Previous, page.Next);
    }

    public async Task AdicionarAsync(TipoProcesso tipo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tipo);
        await _dbContext.TiposProcesso.AddAsync(tipo, cancellationToken).ConfigureAwait(false);
    }

    public Task<bool> CodigoExisteAsync(string codigo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(codigo);
        return _dbContext.TiposProcesso.AsNoTracking().AnyAsync(tipo => tipo.Codigo == codigo, cancellationToken);
    }
}
