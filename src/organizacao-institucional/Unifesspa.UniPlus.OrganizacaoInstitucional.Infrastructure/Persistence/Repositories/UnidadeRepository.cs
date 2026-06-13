namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
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
        FiltroListagemUnidades filtro,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        IQueryable<Unidade> query = _dbContext.Unidades.AsNoTracking();

        if (filtro.TemBusca)
        {
            // Busca acento/caixa-insensível sobre o índice desnormalizado
            // mantido pelo agregado. O termo já chega normalizado pelo handler
            // (mesmo NormalizadorTermoBusca); ambos os lados em maiúsculas e sem
            // diacríticos, então Contains (LIKE '%termo%') basta. Npgsql escapa
            // os curingas do parâmetro automaticamente (issue #640).
            string termo = filtro.TermoBuscaNormalizado!;
            query = query.Where(u => u.BuscaNormalizada.Contains(termo));
        }

        if (filtro.TemTipos)
        {
            // Tipos: tipo = ANY(@tipos). EF aplica o value converter (enum→texto)
            // aos elementos do array — Npgsql gera `tipo = ANY(...)`.
            IReadOnlyList<TipoUnidade> tipos = filtro.Tipos;
            query = query.Where(u => tipos.Contains(u.Tipo));
        }

        query = query.OrderBy(u => u.Id);

        if (afterId is { } cursor)
        {
            // Keyset coerente server-side (ADR-0026 + ADR-0032): Npgsql traduz
            // Guid.CompareTo para o operador uuid > nativo do PG — mesmo
            // comparador do OrderBy(Id). Com Guid v7, a ordem por Id reflete a
            // criação temporal. Aplicado APÓS os filtros: a janela avança sobre
            // o conjunto já filtrado.
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
        // Espelha a normalização do agregado (Trim + ToUpperInvariant) para que
        // " abc " case com o "ABC" persistido — senão a checagem erra e a colisão
        // só estoura no índice único (500 em vez do 409 SiglaJaExiste).
        string siglaNorm = sigla.Trim().ToUpperInvariant();
        return _dbContext.Unidades
            .AsNoTracking()
            .Where(u => excluirId == null || u.Id != excluirId)
            .AnyAsync(u => u.Sigla == siglaNorm, cancellationToken);
    }

    public Task<bool> CodigoExisteEntreLivosAsync(string codigo, Guid? excluirId, CancellationToken cancellationToken)
    {
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
        while (atual.HasValue)
        {
            Guid atualId = atual.Value;
            if (atualId == possivelAncestralId)
            {
                return true;
            }

            Guid? superiorDoAtual = await _dbContext.Unidades
                .AsNoTracking()
                .Where(u => u.Id == atualId)
                .Select(u => u.UnidadeSuperiorId)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            atual = superiorDoAtual;
        }

        return false;
    }
}
