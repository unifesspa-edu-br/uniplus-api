namespace Unifesspa.UniPlus.Ingresso.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Ingresso.Domain.Entities;
using Unifesspa.UniPlus.Ingresso.Domain.Interfaces;

public sealed class MatriculaRepository : IMatriculaRepository
{
    private readonly IngressoDbContext _context;

    public MatriculaRepository(IngressoDbContext context)
    {
        _context = context;
    }

    public async Task<Matricula?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Matriculas
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Matricula>> ObterTodosAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Matriculas
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AdicionarAsync(Matricula entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        await _context.Matriculas.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public void Atualizar(Matricula entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        _context.Matriculas.Update(entity);
    }

    public void Remover(Matricula entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        entity.MarkAsDeleted("system");
    }

    public async Task<Matricula?> ObterComDocumentosAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Matriculas
            .Include(m => m.Documentos)
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }
}
