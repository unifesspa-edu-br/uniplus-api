namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using System.Linq.Expressions;

using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;

using Microsoft.EntityFrameworkCore;

public sealed class DocumentoAPublicarRepository : IDocumentoAPublicarRepository
{
    private readonly SelecaoDbContext _context;

    public DocumentoAPublicarRepository(SelecaoDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public async Task<DocumentoAPublicar?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.DocumentosAPublicar
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<DocumentoAPublicar>> ObterTodosAsync(CancellationToken cancellationToken = default)
    {
        return await _context.DocumentosAPublicar
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<DocumentoAPublicar>> ListarPorProcessoAsync(
        Guid processoSeletivoId,
        CancellationToken cancellationToken = default)
    {
        return await _context.DocumentosAPublicar
            .AsNoTracking()
            .Where(d => d.ProcessoSeletivoId == processoSeletivoId)
            .OrderByDescending(d => d.CreatedAt)
            .ThenByDescending(d => d.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AdicionarAsync(DocumentoAPublicar entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        await _context.DocumentosAPublicar.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public void Atualizar(DocumentoAPublicar entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        _context.DocumentosAPublicar.Update(entity);
    }

    public async Task<bool> TentarReivindicarConfirmacaoAsync(Guid id, CancellationToken cancellationToken = default)
    {
        int linhasAfetadas = await _context.DocumentosAPublicar
            .Where(d => d.Id == id && d.Status == StatusArquivoEnviado.Pendente)
            .ExecuteUpdateAsync(setters => setters.SetProperty(d => d.Status, StatusArquivoEnviado.Confirmado), cancellationToken)
            .ConfigureAwait(false);

        return linhasAfetadas == 1;
    }

    public async Task<IReadOnlyList<ArquivoPendenteVencido>> ListarPendentesVencidosAsync(
        DateTimeOffset agora, int limite, CancellationToken cancellationToken = default)
    {
        return await _context.DocumentosAPublicar
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
        int linhasAfetadas = await _context.DocumentosAPublicar
            .Where(d => d.Id == id)
            .Where(PendenteVencido(agora))
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        return linhasAfetadas == 1;
    }

    public void Remover(DocumentoAPublicar entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        // Sem soft-delete (EntityBase puro — DocumentoAPublicar não é ISoftDeletable).
        // Nenhum caso de uso remove documento pela entidade rastreada: o pendente
        // vencido sai por RemoverSePendenteVencidoAsync, condicionado no banco.
        // Implementado por completude do contrato IRepository<T>.
        _context.DocumentosAPublicar.Remove(entity);
    }

    private static Expression<Func<DocumentoAPublicar, bool>> PendenteVencido(DateTimeOffset agora) =>
        d => d.Status == StatusArquivoEnviado.Pendente && d.ExpiraEm <= agora;
}
