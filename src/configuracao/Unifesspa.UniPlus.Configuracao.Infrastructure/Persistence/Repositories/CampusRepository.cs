namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Pagination;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via DI em ConfiguracaoInfrastructureRegistration.")]
public sealed class CampusRepository : ICampusRepository
{
    private readonly ConfiguracaoDbContext _dbContext;

    public CampusRepository(ConfiguracaoDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public Task<Campus?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.Campi
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public Task<Campus?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.Campi
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<(IReadOnlyList<Campus> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken)
    {
        // Keyset bidirecional (ADR-0089): ordenação, âncora, probe n+1, reversão e
        // flags ficam no helper. Npgsql traduz Guid.CompareTo para o operador uuid
        // nativo do PG (ADR-0026 + ADR-0032).
        CursorKeysetPage<Campus> page = await CursorKeyset
            .ApplyAsync(_dbContext.Campi.AsNoTracking(), afterId, limit, direction, cancellationToken)
            .ConfigureAwait(false);

        return (page.Items, page.PrevAfterId, page.NextAfterId);
    }

    public async Task AdicionarAsync(Campus campus, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(campus);
        await _dbContext.Campi.AddAsync(campus, cancellationToken).ConfigureAwait(false);
    }

    public void Remover(Campus campus)
    {
        ArgumentNullException.ThrowIfNull(campus);
        _dbContext.Campi.Remove(campus);
    }

    public Task<bool> SiglaExisteEntreLivosAsync(string sigla, Guid? excluirId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sigla);

        // Espelha a normalização do agregado (Trim + ToUpperInvariant) para que
        // " camar " case com o "CAMAR" persistido.
        string siglaNorm = sigla.Trim().ToUpperInvariant();
        return _dbContext.Campi
            .AsNoTracking()
            .Where(c => excluirId == null || c.Id != excluirId)
            .AnyAsync(c => c.Sigla == siglaNorm, cancellationToken);
    }

    public Task<bool> ExisteVivoAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.Campi
            .AsNoTracking()
            .AnyAsync(c => c.Id == id, cancellationToken);
    }
}
