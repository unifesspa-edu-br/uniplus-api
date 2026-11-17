namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Interfaces;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via DI em OrganizacaoInstitucionalInfrastructureRegistration.")]
public sealed class UnidadeRepository : IUnidadeRepository
{
    private readonly UniPlusDbContext _dbContext;

    public UnidadeRepository(UniPlusDbContext dbContext)
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

    public async Task<(IReadOnlyList<Unidade> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        FiltroListagemUnidades filtro,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        IQueryable<Unidade> query = _dbContext.Unidades.AsNoTracking().Filtradas(filtro);

        // Keyset bidirecional (ADR-0089): ordenação, âncora, probe n+1, reversão
        // e flags ficam no helper, que opera sobre a query JÁ FILTRADA — os
        // EXISTS do helper herdam os mesmos filtros (busca/tipos). Npgsql traduz
        // Guid.CompareTo para o operador uuid nativo do PG (ADR-0026 + ADR-0032).
        CursorKeysetPage<Unidade> page = await CursorKeyset
            .ApplyAsync(query, afterId, limit, direction, cancellationToken)
            .ConfigureAwait(false);

        return (page.Items, page.PrevAfterId, page.NextAfterId);
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

    public Task<bool> SlugExisteEntreVivosAsync(Slug slug, Guid? excluirId, CancellationToken cancellationToken)
    {
        return _dbContext.Unidades
            .AsNoTracking()
            .Where(u => excluirId == null || u.Id != excluirId)
            .AnyAsync(u => u.Slug == slug, cancellationToken);
    }

    public Task<bool> SiglaExisteEntreVivosAsync(string sigla, Guid? excluirId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sigla);

        // Espelha a normalização do agregado (Trim + ToUpperInvariant) para que
        // " abc " case com o "ABC" persistido — senão a checagem erra e a colisão
        // só estoura no índice único (500 em vez do 409 SiglaJaExiste).
        string siglaNorm = sigla.Trim().ToUpperInvariant();
        return _dbContext.Unidades
            .AsNoTracking()
            .Where(u => excluirId == null || u.Id != excluirId)
            .AnyAsync(u => u.Sigla == siglaNorm, cancellationToken);
    }

    public Task<bool> CodigoExisteEntreVivosAsync(string codigo, Guid? excluirId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(codigo);

        // Espelha o Trim do agregado (mesma razão da Sigla).
        string codigoNorm = codigo.Trim();
        return _dbContext.Unidades
            .AsNoTracking()
            .Where(u => excluirId == null || u.Id != excluirId)
            .AnyAsync(u => u.Codigo == codigoNorm, cancellationToken);
    }

    public Task<bool> PossuiSubordinadasVivasAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.Unidades
            .AsNoTracking()
            .AnyAsync(u => u.UnidadeSuperiorId == id, cancellationToken);
    }

    /// <summary>
    /// Indica se <paramref name="possivelDescendenteId"/> é descendente (ou igual)
    /// de <paramref name="possivelAncestralId"/>, percorrendo a cadeia de
    /// superiores do possível descendente até a raiz. A operação é O(profundidade)
    /// — na prática ≤6 níveis na Unifesspa.
    /// </summary>
    public async Task<bool> EhDescendenteAsync(
        Guid possivelDescendenteId,
        Guid possivelAncestralId,
        CancellationToken cancellationToken)
    {
        Guid? atual = possivelDescendenteId;
        while (atual is { } atualId)
        {
            if (atualId == possivelAncestralId)
            {
                return true;
            }

            atual = await SuperiorDeAsync(atualId, cancellationToken).ConfigureAwait(false);
        }

        return false;
    }

    private Task<Guid?> SuperiorDeAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.Unidades
            .AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => u.UnidadeSuperiorId)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
