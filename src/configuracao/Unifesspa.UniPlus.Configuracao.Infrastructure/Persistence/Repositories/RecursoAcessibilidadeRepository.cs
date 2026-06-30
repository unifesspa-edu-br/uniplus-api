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
public sealed class RecursoAcessibilidadeRepository : IRecursoAcessibilidadeRepository
{
    private readonly ConfiguracaoDbContext _dbContext;

    public RecursoAcessibilidadeRepository(ConfiguracaoDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public Task<RecursoAcessibilidade?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.RecursosAcessibilidade
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public Task<RecursoAcessibilidade?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.RecursosAcessibilidade
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<(IReadOnlyList<RecursoAcessibilidade> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken)
    {
        // Keyset bidirecional (ADR-0089): ordenação por Id (Guid v7, ADR-0026/0032).
        CursorKeysetPage<RecursoAcessibilidade> page = await CursorKeyset
            .ApplyAsync(_dbContext.RecursosAcessibilidade.AsNoTracking(), afterId, limit, direction, cancellationToken)
            .ConfigureAwait(false);

        return (page.Items, page.PrevAfterId, page.NextAfterId);
    }

    public async Task AdicionarAsync(RecursoAcessibilidade recurso, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recurso);
        await _dbContext.RecursosAcessibilidade.AddAsync(recurso, cancellationToken).ConfigureAwait(false);
    }

    public void Remover(RecursoAcessibilidade recurso)
    {
        ArgumentNullException.ThrowIfNull(recurso);
        _dbContext.RecursosAcessibilidade.Remove(recurso);
    }

    public Task<bool> NomeExisteEntreVivosAsync(
        string nome,
        Guid? excluirId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(nome);

        // Espelha a normalização do agregado (Trim) para casar com o valor persistido.
        // Comparação case-sensitive (default do Postgres) — alinhada ao índice único.
        string nomeNorm = nome.Trim();

        return _dbContext.RecursosAcessibilidade
            .AsNoTracking()
            .Where(r => excluirId == null || r.Id != excluirId)
            .AnyAsync(r => r.Nome == nomeNorm, cancellationToken);
    }
}
