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
public sealed class CalendarioDiasUteisRepository : ICalendarioDiasUteisRepository
{
    private readonly ConfiguracaoDbContext _dbContext;

    public CalendarioDiasUteisRepository(ConfiguracaoDbContext dbContext)
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

    public async Task<(IReadOnlyList<CalendarioDiasUteis> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken)
    {
        // Keyset bidirecional (ADR-0089): ordenação por Id (Guid v7, ADR-0026/0032).
        // Sem Include de DiasNaoUteis — a listagem projeta só o cabeçalho do dataset.
        CursorKeysetPage<CalendarioDiasUteis> page = await CursorKeyset
            .ApplyAsync(_dbContext.CalendariosDiasUteis.AsNoTracking(), afterId, limit, direction, cancellationToken)
            .ConfigureAwait(false);

        return (page.Items, page.PrevAfterId, page.NextAfterId);
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
}
