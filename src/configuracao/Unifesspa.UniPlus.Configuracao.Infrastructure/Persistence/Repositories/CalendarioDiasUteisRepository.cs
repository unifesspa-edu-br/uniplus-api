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
public sealed class CalendarioDiasUteisRepository : ICalendarioDiasUteisRepository
{
    private readonly UniPlusDbContext _dbContext;

    public CalendarioDiasUteisRepository(UniPlusDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public Task<CalendarioDiasUteis?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.CalendariosDiasUteis
            .Include(c => c.DiasNaoUteis)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public Task<CalendarioDiasUteis?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.CalendariosDiasUteis
            .AsNoTracking()
            .Include(c => c.DiasNaoUteis)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<(IReadOnlyList<CalendarioDiasUteis> Itens, (string SortKey, Guid Id)? Anterior, (string SortKey, Guid Id)? Proximo)> ListarPaginadoAsync(
        string? afterSortKey,
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken)
    {
        // Keyset bidirecional (ADR-0089): ordenação por Id (Guid v7, ADR-0026/0032).
        // Sem Include de DiasNaoUteis — a listagem projeta só o cabeçalho do calendário.
        OrderedKeysetPage<CalendarioDiasUteis> page = await CursorKeyset
            .ApplyAsync(_dbContext.CalendariosDiasUteis.AsNoTracking(), CondicoesDoFiltro.Nenhuma, afterSortKey, afterId, limit, direction, cancellationToken)
            .ConfigureAwait(false);

        return (page.Items, page.Previous, page.Next);
    }

    public Task<CalendarioDiasUteis?> ObterVigenteAsync(CancellationToken cancellationToken)
    {
        return _dbContext.CalendariosDiasUteis
            .FirstOrDefaultAsync(c => c.Vigente, cancellationToken);
    }

    public async Task AdicionarAsync(CalendarioDiasUteis calendario, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(calendario);
        await _dbContext.CalendariosDiasUteis.AddAsync(calendario, cancellationToken).ConfigureAwait(false);
    }

    public void Remover(CalendarioDiasUteis calendario)
    {
        ArgumentNullException.ThrowIfNull(calendario);
        _dbContext.CalendariosDiasUteis.Remove(calendario);
    }

    public void RegistrarInclusaoDeDiaNaoUtil(CalendarioDiasUteis calendario, DiaNaoUtil dia)
    {
        ArgumentNullException.ThrowIfNull(calendario);
        ArgumentNullException.ThrowIfNull(dia);
        _dbContext.Set<DiaNaoUtil>().Add(dia);
        _dbContext.Entry(calendario).State = EntityState.Modified;
    }
}
