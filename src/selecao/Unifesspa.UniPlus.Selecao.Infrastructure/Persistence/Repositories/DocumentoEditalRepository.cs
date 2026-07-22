namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;

using Microsoft.EntityFrameworkCore;

public sealed class DocumentoEditalRepository : IDocumentoEditalRepository
{
    private readonly SelecaoDbContext _context;

    public DocumentoEditalRepository(SelecaoDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public async Task<DocumentoEdital?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.DocumentosEdital
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<DocumentoEdital>> ObterTodosAsync(CancellationToken cancellationToken = default)
    {
        return await _context.DocumentosEdital
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AdicionarAsync(DocumentoEdital entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        await _context.DocumentosEdital.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public void Atualizar(DocumentoEdital entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        _context.DocumentosEdital.Update(entity);
    }

    public async Task<bool> TentarReivindicarConfirmacaoAsync(Guid id, CancellationToken cancellationToken = default)
    {
        int linhasAfetadas = await _context.DocumentosEdital
            .Where(d => d.Id == id && d.Status == StatusDocumentoEdital.Pendente)
            .ExecuteUpdateAsync(setters => setters.SetProperty(d => d.Status, StatusDocumentoEdital.Confirmado), cancellationToken)
            .ConfigureAwait(false);

        return linhasAfetadas == 1;
    }

    public void Remover(DocumentoEdital entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        // Sem soft-delete (EntityBase puro — DocumentoEdital não é ISoftDeletable):
        // não há caso de uso hoje que remova documento (pendentes expirados são
        // stub de limpeza futura, ver issue #784); implementado por completude do
        // contrato IRepository<T>.
        _context.DocumentosEdital.Remove(entity);
    }
}
