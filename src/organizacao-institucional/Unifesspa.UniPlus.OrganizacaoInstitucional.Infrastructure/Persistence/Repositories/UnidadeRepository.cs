namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Interfaces;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via DI em OrganizacaoInstitucionalInfrastructureRegistration.")]
internal sealed class UnidadeRepository : IUnidadeRepository
{
    private readonly OrganizacaoInstitucionalDbContext _dbContext;

    public UnidadeRepository(OrganizacaoInstitucionalDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public Task<Unidade?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.Unidades
            .Include(u => u.Historico)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public Task<Unidade?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.Unidades
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Unidade>> ListarPaginadoAsync(
        Guid? afterId,
        int take,
        CancellationToken cancellationToken)
    {
        IQueryable<Unidade> query = _dbContext.Unidades
            .AsNoTracking()
            .OrderBy(u => u.Id);

        if (afterId is { } cursor)
        {
            // Keyset coerente server-side (ADR-0026 + ADR-0032): Npgsql traduz
            // Guid.CompareTo para o operador uuid > nativo do PG — mesmo
            // comparador do OrderBy(Id). Com Guid v7, a ordem por Id reflete a
            // criação temporal.
            query = query.Where(u => u.Id.CompareTo(cursor) > 0);
        }

        return await query
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AdicionarAsync(Unidade unidade, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(unidade);
        await _dbContext.Unidades.AddAsync(unidade, cancellationToken).ConfigureAwait(false);
    }

    public void Remover(Unidade unidade)
    {
        ArgumentNullException.ThrowIfNull(unidade);
        _dbContext.Unidades.Remove(unidade);
    }

    public Task<bool> SlugExisteEntreLivosAsync(Slug slug, Guid? excluirId, CancellationToken cancellationToken)
    {
        return _dbContext.Unidades
            .AsNoTracking()
            .Where(u => excluirId == null || u.Id != excluirId)
            .AnyAsync(u => u.Slug == slug, cancellationToken);
    }

    public Task<bool> SiglaExisteEntreLivosAsync(string sigla, Guid? excluirId, CancellationToken cancellationToken)
    {
        string siglaNorm = sigla.ToUpperInvariant();
        return _dbContext.Unidades
            .AsNoTracking()
            .Where(u => excluirId == null || u.Id != excluirId)
            .AnyAsync(u => u.Sigla == siglaNorm, cancellationToken);
    }

    public Task<bool> CodigoExisteEntreLivosAsync(string codigo, Guid? excluirId, CancellationToken cancellationToken)
    {
        return _dbContext.Unidades
            .AsNoTracking()
            .Where(u => excluirId == null || u.Id != excluirId)
            .AnyAsync(u => u.Codigo == codigo, cancellationToken);
    }

    public Task<bool> PossuiSubordinadasVivasAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.Unidades
            .AsNoTracking()
            .AnyAsync(u => u.UnidadeSuperiorId == id, cancellationToken);
    }

    /// <summary>
    /// Determina se definir <paramref name="candidatoSuperiorId"/> como superior de
    /// <paramref name="unidadeId"/> formaria ciclo, percorrendo os ancestrais do
    /// candidato até a raiz. A operação é O(profundidade) — na prática ≤6 níveis
    /// na Unifesspa.
    /// </summary>
    public async Task<bool> FormariaCicloAsync(
        Guid unidadeId,
        Guid candidatoSuperiorId,
        CancellationToken cancellationToken)
    {
        Guid? atual = candidatoSuperiorId;
        while (atual.HasValue)
        {
            if (atual.Value == unidadeId)
            {
                return true;
            }

            Guid? superiorDoAtual = await _dbContext.Unidades
                .AsNoTracking()
                .Where(u => u.Id == atual.Value)
                .Select(u => u.UnidadeSuperiorId)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            atual = superiorDoAtual;
        }

        return false;
    }
}
