namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Pagination;

public sealed class TipoEtapaRepository : ITipoEtapaRepository
{
    private readonly ConfiguracaoDbContext _dbContext;

    public TipoEtapaRepository(ConfiguracaoDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public Task<TipoEtapa?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken) =>
        _dbContext.TiposEtapa.FirstOrDefaultAsync(tipo => tipo.Id == id, cancellationToken);

    public Task<TipoEtapa?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken) =>
        _dbContext.TiposEtapa.AsNoTracking().FirstOrDefaultAsync(tipo => tipo.Id == id, cancellationToken);

    public async Task<(IReadOnlyList<TipoEtapa> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        Guid? afterId, int limit, PaginationDirection direction, CancellationToken cancellationToken)
    {
        CursorKeysetPage<TipoEtapa> page = await CursorKeyset
            .ApplyAsync(_dbContext.TiposEtapa.AsNoTracking().Where(tipo => tipo.Ativo), afterId, limit, direction, cancellationToken)
            .ConfigureAwait(false);
        return (page.Items, page.PrevAfterId, page.NextAfterId);
    }

    public async Task AdicionarAsync(TipoEtapa tipo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tipo);
        await _dbContext.TiposEtapa.AddAsync(tipo, cancellationToken).ConfigureAwait(false);
    }

    public Task<bool> CodigoExisteAsync(string codigo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(codigo);
        return _dbContext.TiposEtapa.AsNoTracking()
            .AnyAsync(tipo => tipo.Codigo == codigo.Trim(), cancellationToken);
    }
}
