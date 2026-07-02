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
public sealed class OfertaCursoRepository : IOfertaCursoRepository
{
    private readonly ConfiguracaoDbContext _dbContext;

    public OfertaCursoRepository(ConfiguracaoDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public Task<OfertaCurso?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.OfertasCurso
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    public Task<OfertaCurso?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.OfertasCurso
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    public async Task<(IReadOnlyList<OfertaCurso> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken)
    {
        // Keyset bidirecional (ADR-0089): ordenação por Id (Guid v7, ADR-0026/0032).
        CursorKeysetPage<OfertaCurso> page = await CursorKeyset
            .ApplyAsync(_dbContext.OfertasCurso.AsNoTracking(), afterId, limit, direction, cancellationToken)
            .ConfigureAwait(false);

        return (page.Items, page.PrevAfterId, page.NextAfterId);
    }

    public async Task AdicionarAsync(OfertaCurso ofertaCurso, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ofertaCurso);
        await _dbContext.OfertasCurso.AddAsync(ofertaCurso, cancellationToken).ConfigureAwait(false);
    }

    public void Remover(OfertaCurso ofertaCurso)
    {
        ArgumentNullException.ThrowIfNull(ofertaCurso);
        _dbContext.OfertasCurso.Remove(ofertaCurso);
    }
}
