namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Pagination;

public sealed class TipoProcessoRepository : ITipoProcessoRepository
{
    private readonly ConfiguracaoDbContext _dbContext;

    public TipoProcessoRepository(ConfiguracaoDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public Task<TipoProcesso?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken) =>
        _dbContext.TiposProcesso.FirstOrDefaultAsync(tipo => tipo.Id == id, cancellationToken);

    public Task<TipoProcesso?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken) =>
        _dbContext.TiposProcesso.AsNoTracking().FirstOrDefaultAsync(tipo => tipo.Id == id, cancellationToken);

    public async Task<(IReadOnlyList<TipoProcesso> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        Guid? afterId, int limit, PaginationDirection direction, bool apenasAtivos, CancellationToken cancellationToken)
    {
        IQueryable<TipoProcesso> consulta = _dbContext.TiposProcesso.AsNoTracking();
        if (apenasAtivos)
        {
            consulta = consulta.Where(tipo => tipo.Ativo);
        }

        CursorKeysetPage<TipoProcesso> page = await CursorKeyset
            .ApplyAsync(consulta, afterId, limit, direction, cancellationToken)
            .ConfigureAwait(false);
        return (page.Items, page.PrevAfterId, page.NextAfterId);
    }

    public async Task AdicionarAsync(TipoProcesso tipo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tipo);
        await _dbContext.TiposProcesso.AddAsync(tipo, cancellationToken).ConfigureAwait(false);
    }

    public Task<bool> CodigoExisteAsync(string codigo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(codigo);
        return _dbContext.TiposProcesso.AsNoTracking()
            .AnyAsync(tipo => tipo.Codigo == codigo.Trim(), cancellationToken);
    }
}
