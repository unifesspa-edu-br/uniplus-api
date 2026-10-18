namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using Domain.Entities;
using Domain.Interfaces;

using Microsoft.EntityFrameworkCore;

public sealed class AtoDoProcessoRepository : IAtoDoProcessoRepository
{
    private readonly SelecaoDbContext _context;

    public AtoDoProcessoRepository(SelecaoDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public async Task AdicionarAsync(AtoDoProcesso ato, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ato);
        await _context.AtosDoProcesso.AddAsync(ato, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AtoDoProcesso?> ObterParaMutacaoAsync(Guid atoId, CancellationToken cancellationToken = default)
    {
        return await _context.AtosDoProcesso
            .FirstOrDefaultAsync(a => a.Id == atoId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<AtoDoProcesso?> ObterComAnexosAsync(Guid atoId, CancellationToken cancellationToken = default)
    {
        return await _context.AtosDoProcesso
            .AsNoTracking()
            .Include(a => a.Anexos)
            .FirstOrDefaultAsync(a => a.Id == atoId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<AtoDoProcesso>> ListarDoProcessoAsync(
        Guid processoSeletivoId,
        CancellationToken cancellationToken = default)
    {
        return await _context.AtosDoProcesso
            .AsNoTracking()
            .Include(a => a.Anexos)
            .Where(a => a.ProcessoSeletivoId == processoSeletivoId)
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .AsSplitQuery()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
