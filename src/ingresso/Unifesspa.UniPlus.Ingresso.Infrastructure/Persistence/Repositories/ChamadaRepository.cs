namespace Unifesspa.UniPlus.Ingresso.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Domain.Entities;
using Domain.Interfaces;

public sealed class ChamadaRepository : IChamadaRepository
{
    private readonly IngressoDbContext _context;

    public ChamadaRepository(IngressoDbContext context)
    {
        _context = context;
    }

    public async Task<Chamada?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Chamadas
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Chamada>> ObterTodosAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Chamadas
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AdicionarAsync(Chamada entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        await _context.Chamadas.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public void Atualizar(Chamada entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        _context.Chamadas.Update(entity);
    }

    public void Remover(Chamada entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        entity.MarkAsDeleted("system");
    }

    public async Task<Chamada?> ObterComConvocacoesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Chamadas
            .Include(c => c.Convocacoes)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> ObterProximoNumeroChamadaAsync(Guid editalId, CancellationToken cancellationToken = default)
    {
        int ultimoNumero = await _context.Chamadas
            .Where(c => c.EditalId == editalId)
            .Select(c => c.Numero)
            .DefaultIfEmpty(0)
            .MaxAsync(cancellationToken)
            .ConfigureAwait(false);

        return ultimoNumero + 1;
    }
}
