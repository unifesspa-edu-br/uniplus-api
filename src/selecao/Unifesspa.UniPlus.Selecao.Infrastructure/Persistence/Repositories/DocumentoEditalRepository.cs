namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using System.Linq.Expressions;

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

    public async Task<IReadOnlyList<DocumentoEdital>> ListarPorProcessoAsync(
        Guid processoSeletivoId,
        CancellationToken cancellationToken = default)
    {
        return await _context.DocumentosEdital
            .AsNoTracking()
            .Where(d => d.ProcessoSeletivoId == processoSeletivoId)
            .OrderByDescending(d => d.CreatedAt)
            .ThenByDescending(d => d.Id)
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
            .Where(d => d.Id == id && d.Status == StatusArquivoEnviado.Pendente)
            .ExecuteUpdateAsync(setters => setters.SetProperty(d => d.Status, StatusArquivoEnviado.Confirmado), cancellationToken)
            .ConfigureAwait(false);

        return linhasAfetadas == 1;
    }

    public async Task<IReadOnlyList<ArquivoPendenteVencido>> ListarPendentesVencidosAsync(
        DateTimeOffset agora, int limite, CancellationToken cancellationToken = default)
    {
        return await _context.DocumentosEdital
            .AsNoTracking()
            .Where(PendenteVencido(agora))
            .OrderBy(d => d.ExpiraEm)
            .ThenBy(d => d.Id)
            .Take(limite)
            .Select(d => new ArquivoPendenteVencido(d.Id, d.ObjectKey))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> RemoverSePendenteVencidoAsync(Guid id, DateTimeOffset agora, CancellationToken cancellationToken = default)
    {
        int linhasAfetadas = await _context.DocumentosEdital
            .Where(d => d.Id == id)
            .Where(PendenteVencido(agora))
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        return linhasAfetadas == 1;
    }

    public void Remover(DocumentoEdital entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        // Sem soft-delete (EntityBase puro — DocumentoEdital não é ISoftDeletable).
        // Nenhum caso de uso remove documento pela entidade rastreada: o pendente
        // vencido sai por RemoverSePendenteVencidoAsync, condicionado no banco.
        // Implementado por completude do contrato IRepository<T>.
        _context.DocumentosEdital.Remove(entity);
    }

    private static Expression<Func<DocumentoEdital, bool>> PendenteVencido(DateTimeOffset agora) =>
        d => d.Status == StatusArquivoEnviado.Pendente && d.ExpiraEm <= agora;
}
