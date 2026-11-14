namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using Domain.Entities;
using Domain.Interfaces;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

public sealed class GeracaoDeEnvioAoInepRepository : IGeracaoDeEnvioAoInepRepository
{
    private readonly UniPlusDbContext _context;

    public GeracaoDeEnvioAoInepRepository(UniPlusDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public async Task AdicionarAsync(GeracaoDeEnvioAoInep geracao, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(geracao);
        await _context.GeracoesDeEnvioAoInep.AddAsync(geracao, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<GeracaoDeEnvioAoInep>> ListarAsync(
        Guid processoSeletivoId, Guid etapaProcessoId, string edicaoCodigo, CancellationToken cancellationToken = default)
    {
        return await _context.GeracoesDeEnvioAoInep
            .AsNoTracking()
            .Where(g => g.ProcessoSeletivoId == processoSeletivoId
                && g.EtapaProcessoId == etapaProcessoId
                && g.EdicaoCodigo == edicaoCodigo)
            .OrderByDescending(g => g.GeradoEm)
            .ThenByDescending(g => g.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
