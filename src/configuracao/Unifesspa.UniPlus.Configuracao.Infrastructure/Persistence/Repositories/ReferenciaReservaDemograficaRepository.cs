namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Pagination;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via DI em ConfiguracaoInfrastructureRegistration.")]
public sealed class ReferenciaReservaDemograficaRepository : IReferenciaReservaDemograficaRepository
{
    private readonly UniPlusDbContext _dbContext;

    public ReferenciaReservaDemograficaRepository(UniPlusDbContext dbContext)
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

    public async Task<(IReadOnlyList<ReferenciaReservaDemografica> Itens, (string SortKey, Guid Id)? Anterior, (string SortKey, Guid Id)? Proximo)> ListarPaginadoAsync(
        string? afterSortKey,
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken)
    {
        // Keyset bidirecional (ADR-0089): ordenação por Id (Guid v7, ADR-0026/0032).
        OrderedKeysetPage<ReferenciaReservaDemografica> page = await CursorKeyset
            .ApplyAsync(_dbContext.ReferenciasReservaDemografica.AsNoTracking(), CondicoesDoFiltro.Nenhuma, afterSortKey, afterId, limit, direction, cancellationToken)
            .ConfigureAwait(false);

        return (page.Items, page.Previous, page.Next);
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
        ArgumentNullException.ThrowIfNull(censoReferencia);

        // Espelha a normalização do agregado (Trim) para casar com o valor persistido.
        string censoNorm = censoReferencia.Trim();
        return _dbContext.ReferenciasReservaDemografica
            .AsNoTracking()
            .Where(r => excluirId == null || r.Id != excluirId)
            .AnyAsync(r => r.CensoReferencia == censoNorm, cancellationToken);
    }
}
