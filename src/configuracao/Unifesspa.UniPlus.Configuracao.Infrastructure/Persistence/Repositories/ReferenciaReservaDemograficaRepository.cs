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
internal sealed class ReferenciaReservaDemograficaRepository : IReferenciaReservaDemograficaRepository
{
    private readonly ConfiguracaoDbContext _dbContext;

    public ReferenciaReservaDemograficaRepository(ConfiguracaoDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public Task<ReferenciaReservaDemografica?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.ReferenciasReservaDemografica
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public Task<ReferenciaReservaDemografica?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.ReferenciasReservaDemografica
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<(IReadOnlyList<ReferenciaReservaDemografica> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken)
    {
        // Keyset bidirecional (ADR-0089): ordenação por Id (Guid v7, ADR-0026/0032).
        CursorKeysetPage<ReferenciaReservaDemografica> page = await CursorKeyset
            .ApplyAsync(_dbContext.ReferenciasReservaDemografica.AsNoTracking(), afterId, limit, direction, cancellationToken)
            .ConfigureAwait(false);

        return (page.Items, page.PrevAfterId, page.NextAfterId);
    }

    public async Task AdicionarAsync(ReferenciaReservaDemografica referencia, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(referencia);
        await _dbContext.ReferenciasReservaDemografica.AddAsync(referencia, cancellationToken).ConfigureAwait(false);
    }

    public void Remover(ReferenciaReservaDemografica referencia)
    {
        ArgumentNullException.ThrowIfNull(referencia);
        _dbContext.ReferenciasReservaDemografica.Remove(referencia);
    }

    public Task<bool> CensoExisteEntreLivosAsync(string censoReferencia, Guid? excluirId, CancellationToken cancellationToken)
    {
        // Espelha a normalização do agregado (Trim) para casar com o valor persistido.
        string censoNorm = censoReferencia.Trim();
        return _dbContext.ReferenciasReservaDemografica
            .AsNoTracking()
            .Where(r => excluirId == null || r.Id != excluirId)
            .AnyAsync(r => r.CensoReferencia == censoNorm, cancellationToken);
    }
}
