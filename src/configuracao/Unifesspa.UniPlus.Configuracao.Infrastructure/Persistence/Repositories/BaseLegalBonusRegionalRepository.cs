namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Pagination;

public sealed class BaseLegalBonusRegionalRepository : IBaseLegalBonusRegionalRepository
{
    private readonly ConfiguracaoDbContext _dbContext;

    public BaseLegalBonusRegionalRepository(ConfiguracaoDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public Task<BaseLegalBonusRegional?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.BaseLegaisBonus
            .Include(x => x.Municipios)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public Task<BaseLegalBonusRegional?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.BaseLegaisBonus
            .AsNoTracking()
            .Include(x => x.Municipios)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<(IReadOnlyList<BaseLegalBonusRegional> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken)
    {
        CursorKeysetPage<BaseLegalBonusRegional> page = await CursorKeyset
            .ApplyAsync(_dbContext.BaseLegaisBonus.AsNoTracking().Include(x => x.Municipios), afterId, limit, direction, cancellationToken)
            .ConfigureAwait(false);

        return (page.Items, page.PrevAfterId, page.NextAfterId);
    }

    public async Task AdicionarAsync(BaseLegalBonusRegional entity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entity);
        await _dbContext.BaseLegaisBonus.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public void Remover(BaseLegalBonusRegional entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        _dbContext.BaseLegaisBonus.Remove(entity);
    }
}
