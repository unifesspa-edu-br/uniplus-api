namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using Domain.Entities;
using Domain.Interfaces;

using Microsoft.EntityFrameworkCore;

public sealed class LoteDeNotasDoEnemRepository : ILoteDeNotasDoEnemRepository
{
    private readonly SelecaoDbContext _context;

    public LoteDeNotasDoEnemRepository(SelecaoDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public async Task AdicionarAsync(LoteDeNotasDoEnem lote, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lote);
        await _context.LotesDeNotasDoEnem.AddAsync(lote, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LoteDeNotasDoEnem?> ObterVigenteAsync(
        Guid processoSeletivoId, Guid etapaProcessoId, string edicaoCodigo, CancellationToken cancellationToken = default)
    {
        return await DoEscopo(processoSeletivoId, etapaProcessoId, edicaoCodigo)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<LoteDeNotasDoEnem?> ObterComOcorrenciasAsync(
        Guid processoSeletivoId,
        Guid etapaProcessoId,
        string edicaoCodigo,
        Guid loteId,
        CancellationToken cancellationToken = default)
    {
        return await DoEscopo(processoSeletivoId, etapaProcessoId, edicaoCodigo)
            .Include(l => l.Ocorrencias)
            .Where(l => l.Id == loteId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<LoteDeNotasDoEnem>> ListarAsync(
        Guid processoSeletivoId, Guid etapaProcessoId, string edicaoCodigo, CancellationToken cancellationToken = default)
    {
        return await DoEscopo(processoSeletivoId, etapaProcessoId, edicaoCodigo)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> ObterNumerosDeInscricaoAsync(
        IReadOnlyCollection<Guid> inscricaoIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inscricaoIds);

        Guid[] ids = [.. inscricaoIds];
        var numeros = await _context.Inscricoes
            .AsNoTracking()
            .Where(i => ids.Contains(i.Id) && i.NumeroInscricao != null)
            .Select(i => new { i.Id, Numero = i.NumeroInscricao! })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return numeros.ToDictionary(static n => n.Id, static n => n.Numero);
    }

    /// <summary>Os lotes do escopo, sem rastreamento, do mais recente para o mais antigo.</summary>
    private IQueryable<LoteDeNotasDoEnem> DoEscopo(Guid processoSeletivoId, Guid etapaProcessoId, string edicaoCodigo) =>
        _context.LotesDeNotasDoEnem
            .AsNoTracking()
            .Where(l => l.ProcessoSeletivoId == processoSeletivoId
                && l.EtapaProcessoId == etapaProcessoId
                && l.EdicaoCodigo == edicaoCodigo)
            .OrderByDescending(l => l.ImportadoEm)
            .ThenByDescending(l => l.Id);
}
