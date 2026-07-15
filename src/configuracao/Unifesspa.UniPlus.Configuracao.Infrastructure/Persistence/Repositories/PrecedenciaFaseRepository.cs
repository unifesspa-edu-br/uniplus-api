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
public sealed class PrecedenciaFaseRepository : IPrecedenciaFaseRepository
{
    private readonly ConfiguracaoDbContext _dbContext;

    public PrecedenciaFaseRepository(ConfiguracaoDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public Task<PrecedenciaFase?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.PrecedenciasFase
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public Task<PrecedenciaFase?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.PrecedenciasFase
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<(IReadOnlyList<PrecedenciaFase> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken)
    {
        // Keyset bidirecional (ADR-0089): ordenação por Id (Guid v7, ADR-0026/0032).
        CursorKeysetPage<PrecedenciaFase> page = await CursorKeyset
            .ApplyAsync(_dbContext.PrecedenciasFase.AsNoTracking(), afterId, limit, direction, cancellationToken)
            .ConfigureAwait(false);

        return (page.Items, page.PrevAfterId, page.NextAfterId);
    }

    public async Task<IReadOnlyList<PrecedenciaFase>> ListarVivasAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.PrecedenciasFase
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    // Advisory lock com escopo de transação (pg_advisory_xact_lock): adquirido na
    // conexão/transação ambiente do handler (o mesmo DbContext que fará o
    // SaveChanges do outbox transacional, ADR-0004) e liberado automaticamente no
    // commit ou rollback — nenhuma chave a destravar manualmente, nenhum risco de
    // lock vazado por exceção. A chave é fixa (hashtext do nome do cadastro): o
    // objetivo é serializar toda escrita no grafo de precedências entre si, não
    // por linha — o problema (ciclo formado por duas arestas distintas) é
    // estrutural do grafo inteiro, não de um par.
    public Task TravarGrafoParaEscritaAsync(CancellationToken cancellationToken) =>
        _dbContext.Database.ExecuteSqlRawAsync(
            "SELECT pg_advisory_xact_lock(hashtext('configuracao.precedencia_fase'));",
            cancellationToken);

    public async Task AdicionarAsync(PrecedenciaFase aresta, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(aresta);
        await _dbContext.PrecedenciasFase.AddAsync(aresta, cancellationToken).ConfigureAwait(false);
    }

    public void Remover(PrecedenciaFase aresta)
    {
        ArgumentNullException.ThrowIfNull(aresta);
        _dbContext.PrecedenciasFase.Remove(aresta);
    }
}
