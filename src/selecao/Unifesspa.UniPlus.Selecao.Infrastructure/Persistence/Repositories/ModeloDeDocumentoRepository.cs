namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;

using Microsoft.EntityFrameworkCore;

public sealed class ModeloDeDocumentoRepository : IModeloDeDocumentoRepository
{
    private readonly SelecaoDbContext _context;

    public ModeloDeDocumentoRepository(SelecaoDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public async Task<ModeloDeDocumento?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _context.ModelosDeDocumento.FirstOrDefaultAsync(m => m.Id == id, cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<ModeloDeDocumento>> ObterTodosAsync(CancellationToken cancellationToken = default) =>
        await _context.ModelosDeDocumento.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task AdicionarAsync(ModeloDeDocumento entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        await _context.ModelosDeDocumento.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public void Atualizar(ModeloDeDocumento entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        _context.ModelosDeDocumento.Update(entity);
    }

    public async Task<bool> TentarReivindicarConfirmacaoAsync(Guid id, CancellationToken cancellationToken = default)
    {
        int linhasAfetadas = await _context.ModelosDeDocumento
            .Where(m => m.Id == id && m.Status == StatusArquivoEnviado.Pendente)
            .ExecuteUpdateAsync(setters => setters.SetProperty(m => m.Status, StatusArquivoEnviado.Confirmado), cancellationToken)
            .ConfigureAwait(false);

        return linhasAfetadas == 1;
    }

    public void Remover(ModeloDeDocumento entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        // Nenhum caso de uso remove modelo; a limpeza dos pendentes vencidos é de outra issue.
        _context.ModelosDeDocumento.Remove(entity);
    }
}
