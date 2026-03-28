namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

public sealed class EditalRepository : IEditalRepository
{
    private readonly SelecaoDbContext _context;

    public EditalRepository(SelecaoDbContext context)
    {
        _context = context;
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
        entity.MarkAsDeleted("system");
    }

    public async Task<Edital?> ObterComEtapasECotasAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Editais
            .Include(e => e.Etapas)
            .Include(e => e.Cotas)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }
}
