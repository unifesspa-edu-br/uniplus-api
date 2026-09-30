namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Pagination;

public sealed class FatoCandidatoRepository : IFatoCandidatoRepository
{
    private readonly ConfiguracaoDbContext _dbContext;

    public FatoCandidatoRepository(ConfiguracaoDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public Task<FatoCandidato?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken) =>
        _dbContext.FatosCandidato.Include(f => f.ValoresDominioDeclarados).FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

    public Task<FatoCandidato?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken) =>
        _dbContext.FatosCandidato.AsNoTracking().Include(f => f.ValoresDominioDeclarados)
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

    public async Task<(IReadOnlyList<FatoCandidato> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        Guid? afterId, int limit, PaginationDirection direction, OrigemFato? origem, bool? ativo, CancellationToken cancellationToken)
    {
        IQueryable<FatoCandidato> consulta = _dbContext.FatosCandidato.AsNoTracking().Include(f => f.ValoresDominioDeclarados);
        if (origem is { } origemFiltro)
        {
            consulta = consulta.Where(f => f.Origem == origemFiltro);
        }

        if (ativo is { } ativoFiltro)
        {
            consulta = consulta.Where(f => f.Ativo == ativoFiltro);
        }

        CursorKeysetPage<FatoCandidato> page = await CursorKeyset
            .ApplyAsync(consulta, afterId, limit, direction, cancellationToken)
            .ConfigureAwait(false);
        return (page.Items, page.PrevAfterId, page.NextAfterId);
    }

    public async Task AdicionarAsync(FatoCandidato fato, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fato);
        await _dbContext.FatosCandidato.AddAsync(fato, cancellationToken).ConfigureAwait(false);
    }

    public Task<bool> CodigoExisteAsync(string codigo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(codigo);
        return _dbContext.FatosCandidato.AsNoTracking().AnyAsync(f => f.Codigo == codigo.Trim(), cancellationToken);
    }
}
