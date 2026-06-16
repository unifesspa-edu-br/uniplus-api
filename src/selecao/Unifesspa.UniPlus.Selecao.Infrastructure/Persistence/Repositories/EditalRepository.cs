namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Domain.Entities;
using Domain.Interfaces;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Pagination;

public sealed class EditalRepository : IEditalRepository
{
    private readonly SelecaoDbContext _context;
    private readonly TimeProvider _timeProvider;

    public EditalRepository(SelecaoDbContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task<Edital?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Editais
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Edital>> ObterTodosAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Editais
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AdicionarAsync(Edital entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        await _context.Editais.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public void Atualizar(Edital entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        _context.Editais.Update(entity);
    }

    public void Remover(Edital entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        entity.MarkAsDeleted("system", _timeProvider.GetUtcNow());
    }

    public async Task<Edital?> ObterComEtapasECotasAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Editais
            .Include(e => e.Etapas)
            .Include(e => e.Cotas)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<(IReadOnlyList<Edital> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken = default)
    {
        // Keyset bidirecional (ADR-0089): ordenação, âncora, probe n+1, reversão
        // e flags ficam no helper. Npgsql traduz Guid.CompareTo para o operador
        // uuid nativo do PG; com Guid v7 (ADR-0032) a ordem por Id é cronológica.
        IQueryable<Edital> query = _context.Editais.AsNoTracking();

        CursorKeysetPage<Edital> page = await CursorKeyset
            .ApplyAsync(query, afterId, limit, direction, cancellationToken)
            .ConfigureAwait(false);

        return (page.Items, page.PrevAfterId, page.NextAfterId);
    }
}
