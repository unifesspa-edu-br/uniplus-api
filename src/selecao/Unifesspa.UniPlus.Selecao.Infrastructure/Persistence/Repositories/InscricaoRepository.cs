namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

public sealed class InscricaoRepository : IInscricaoRepository
{
    private readonly SelecaoDbContext _context;

    public InscricaoRepository(SelecaoDbContext context)
    {
        _context = context;
    }

    public async Task<Inscricao?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Inscricoes
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Inscricao>> ObterTodosAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Inscricoes
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AdicionarAsync(Inscricao entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        await _context.Inscricoes.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public void Atualizar(Inscricao entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        _context.Inscricoes.Update(entity);
    }

    public void Remover(Inscricao entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        entity.MarkAsDeleted("system");
    }

    public async Task<bool> ExisteInscricaoAtivaAsync(Guid candidatoId, Guid editalId, CancellationToken cancellationToken = default)
    {
        return await _context.Inscricoes
            .AnyAsync(i => i.CandidatoId == candidatoId
                       && i.EditalId == editalId
                       && i.Status != StatusInscricao.Cancelada, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Inscricao>> ObterPorEditalAsync(Guid editalId, CancellationToken cancellationToken = default)
    {
        return await _context.Inscricoes
            .Where(i => i.EditalId == editalId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
