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
public sealed class LocalOfertaRepository : ILocalOfertaRepository
{
    private readonly ConfiguracaoDbContext _dbContext;

    public LocalOfertaRepository(ConfiguracaoDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public Task<LocalOferta?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.LocaisOferta
            .FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
    }

    public Task<LocalOferta?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.LocaisOferta
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
    }

    public async Task<(IReadOnlyList<LocalOferta> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken)
    {
        CursorKeysetPage<LocalOferta> page = await CursorKeyset
            .ApplyAsync(_dbContext.LocaisOferta.AsNoTracking(), afterId, limit, direction, cancellationToken)
            .ConfigureAwait(false);

        return (page.Items, page.PrevAfterId, page.NextAfterId);
    }

    public async Task AdicionarAsync(LocalOferta localOferta, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(localOferta);
        await _dbContext.LocaisOferta.AddAsync(localOferta, cancellationToken).ConfigureAwait(false);
    }

    public void Remover(LocalOferta localOferta)
    {
        ArgumentNullException.ThrowIfNull(localOferta);
        _dbContext.LocaisOferta.Remove(localOferta);
    }

    public Task<bool> ExisteVivoComCampusResponsavelAsync(Guid campusResponsavelId, CancellationToken cancellationToken)
    {
        return _dbContext.LocaisOferta
            .AsNoTracking()
            .AnyAsync(l => l.CampusResponsavelId == campusResponsavelId, cancellationToken);
    }

    public Task<bool> ReferenciadoPorOfertaCursoVivaAsync(Guid localOfertaId, CancellationToken cancellationToken)
    {
        // TODO(UNI-REQ-0010): quando a entidade oferta_curso existir no módulo,
        // consultar aqui se há oferta de curso viva referenciando este local de
        // oferta. Até lá, não há tabela a consultar — a checagem retorna false.
        _ = localOfertaId;
        _ = cancellationToken;
        return Task.FromResult(false);
    }
}
