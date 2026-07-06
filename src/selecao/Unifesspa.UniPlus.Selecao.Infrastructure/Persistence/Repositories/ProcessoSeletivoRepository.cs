namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Domain.Entities;
using Domain.Interfaces;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Pagination;

public sealed class ProcessoSeletivoRepository : IProcessoSeletivoRepository
{
    private readonly SelecaoDbContext _context;
    private readonly TimeProvider _timeProvider;

    public ProcessoSeletivoRepository(SelecaoDbContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task<ProcessoSeletivo?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.ProcessosSeletivos
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ProcessoSeletivo>> ObterTodosAsync(CancellationToken cancellationToken = default)
    {
        return await _context.ProcessosSeletivos
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AdicionarAsync(ProcessoSeletivo entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        await _context.ProcessosSeletivos.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public void Atualizar(ProcessoSeletivo entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        _context.ProcessosSeletivos.Update(entity);
    }

    public void Remover(ProcessoSeletivo entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        entity.MarkAsDeleted("system", _timeProvider.GetUtcNow());
    }

    public async Task<ProcessoSeletivo?> ObterComConfiguracaoAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.ProcessosSeletivos
            .Include(p => p.Etapas)
            .Include(p => p.OfertaAtendimento!).ThenInclude(o => o.Condicoes)
            .Include(p => p.OfertaAtendimento!).ThenInclude(o => o.Recursos)
            .Include(p => p.OfertaAtendimento!).ThenInclude(o => o.TiposDeficiencia)
            .Include(p => p.DistribuicaoVagas).ThenInclude(d => d.Modalidades)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<(IReadOnlyList<ProcessoSeletivo> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken = default)
    {
        // Keyset bidirecional (ADR-0089): ordenação, âncora, probe n+1, reversão
        // e flags ficam no helper. Com Guid v7 (ADR-0032) a ordem por Id é cronológica.
        IQueryable<ProcessoSeletivo> query = _context.ProcessosSeletivos.AsNoTracking();

        CursorKeysetPage<ProcessoSeletivo> page = await CursorKeyset
            .ApplyAsync(query, afterId, limit, direction, cancellationToken)
            .ConfigureAwait(false);

        return (page.Items, page.PrevAfterId, page.NextAfterId);
    }
}
