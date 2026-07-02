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
public sealed class CursoRepository : ICursoRepository
{
    private readonly ConfiguracaoDbContext _dbContext;

    public CursoRepository(ConfiguracaoDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public Task<Curso?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.Cursos
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public Task<Curso?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.Cursos
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<(IReadOnlyList<Curso> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken)
    {
        // Keyset bidirecional (ADR-0089): ordenação por Id (Guid v7, ADR-0026/0032).
        CursorKeysetPage<Curso> page = await CursorKeyset
            .ApplyAsync(_dbContext.Cursos.AsNoTracking(), afterId, limit, direction, cancellationToken)
            .ConfigureAwait(false);

        return (page.Items, page.PrevAfterId, page.NextAfterId);
    }

    public async Task AdicionarAsync(Curso curso, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(curso);
        await _dbContext.Cursos.AddAsync(curso, cancellationToken).ConfigureAwait(false);
    }

    public void Remover(Curso curso)
    {
        ArgumentNullException.ThrowIfNull(curso);
        _dbContext.Cursos.Remove(curso);
    }

    public Task<bool> CodigoExisteEntreVivosAsync(
        string codigo,
        Guid? excluirId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(codigo);

        // Espelha a normalização do agregado (Trim) para casar com o valor persistido.
        // Comparação case-sensitive (default do Postgres) — alinhada ao índice único.
        string codigoNorm = codigo.Trim();

        return _dbContext.Cursos
            .AsNoTracking()
            .Where(c => excluirId == null || c.Id != excluirId)
            .AnyAsync(c => c.Codigo == codigoNorm, cancellationToken);
    }

    public Task<bool> ReferenciadoPorOfertaCursoVivaAsync(Guid cursoId, CancellationToken cancellationToken)
    {
        // EXISTS sobre ofertas vivas (#749): o query filter global de soft-delete
        // já restringe a ofertas não removidas — o soft-delete da oferta libera o
        // curso para remoção.
        return _dbContext.OfertasCurso
            .AsNoTracking()
            .AnyAsync(o => o.CursoId == cursoId, cancellationToken);
    }
}
