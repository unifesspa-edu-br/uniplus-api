namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using System.Collections.Generic;
using System.Linq;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Repositório de <see cref="ObrigatoriedadeLegal"/>.
/// </summary>
/// <remarks>
/// Escritas (Adicionar/Atualizar/Remover) e leituras (listagem paginada,
/// vigentes para tipo de processo, colisão de código) ocorrem dentro do
/// <c>SaveChangesAsync</c> do contexto — a captura de histórico append-only
/// é responsabilidade do <c>ObrigatoriedadeLegalHistoricoInterceptor</c>.
/// </remarks>
public sealed class ObrigatoriedadeLegalRepository : IObrigatoriedadeLegalRepository
{
    private readonly UniPlusDbContext _context;

    public ObrigatoriedadeLegalRepository(UniPlusDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public async Task<ObrigatoriedadeLegal?> ObterPorIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _context.ObrigatoriedadesLegais
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ObrigatoriedadeLegal>> ObterTodosAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.ObrigatoriedadesLegais
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AdicionarAsync(
        ObrigatoriedadeLegal entity,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        await _context.ObrigatoriedadesLegais.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public void Atualizar(ObrigatoriedadeLegal entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        _context.ObrigatoriedadesLegais.Update(entity);
    }

    public void Remover(ObrigatoriedadeLegal entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        // SoftDeleteInterceptor converte Deleted → Modified + IsDeleted=true.
        // ObrigatoriedadeLegalHistoricoInterceptor grava a linha do snapshot
        // refletindo o estado pós-desativação.
        _context.ObrigatoriedadesLegais.Remove(entity);
    }

    public async Task<(IReadOnlyList<ObrigatoriedadeLegal> Itens, (string SortKey, Guid Id)? Anterior, (string SortKey, Guid Id)? Proximo)> ListarPaginadoAsync(
        string? afterSortKey,
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        FiltroDasObrigatoriedadesLegais filtro,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        OrderedKeysetPage<ObrigatoriedadeLegal> page = await CursorKeyset
            .ApplyAsync(Filtrar(filtro), CondicoesDe(filtro), afterSortKey, afterId, limit, direction, cancellationToken)
            .ConfigureAwait(false);

        return (page.Items, page.Previous, page.Next);
    }

    private IQueryable<ObrigatoriedadeLegal> Filtrar(FiltroDasObrigatoriedadesLegais filtro)
    {
        IQueryable<ObrigatoriedadeLegal> query = _context.ObrigatoriedadesLegais.AsNoTracking();
        if (TipoDeProcessoFiltrado(filtro) is { } tipoProcessoCodigo)
        {
            query = query.Where(o => o.TipoProcessoCodigo == tipoProcessoCodigo);
        }
        if (filtro.Categoria is { } categoria)
        {
            query = query.Where(o => o.Categoria == categoria);
        }
        return filtro.VigentesEm is { } dia ? FiltrarVigentes(query, dia) : query;
    }

    // O código vazio não recorta.
    private static string? TipoDeProcessoFiltrado(FiltroDasObrigatoriedadesLegais filtro) =>
        string.IsNullOrEmpty(filtro.TipoProcessoCodigo) ? null : filtro.TipoProcessoCodigo;

    private static CondicoesDoFiltro CondicoesDe(FiltroDasObrigatoriedadesLegais filtro) =>
        CondicoesDoFiltro.De(
            ParteDoFiltro.De(TipoDeProcessoFiltrado(filtro)),
            ParteDoFiltro.De(filtro.Categoria),
            ParteDoFiltro.De(filtro.VigentesEm is not null));

    private static IQueryable<ObrigatoriedadeLegal> FiltrarVigentes(IQueryable<ObrigatoriedadeLegal> query, DateOnly dia) =>
        query.Where(o => o.VigenciaInicio <= dia && (o.VigenciaFim == null || o.VigenciaFim > dia));

    public async Task<IReadOnlyList<ObrigatoriedadeLegal>> ObterVigentesParaTipoProcessoAsync(
        string tipoProcessoCodigo,
        DateOnly dataReferencia,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tipoProcessoCodigo);

        // A igualdade exata mantém a referência ao tipo de processo cadastrado.
        return await _context.ObrigatoriedadesLegais
            .AsNoTracking()
            .Where(o =>
                (o.TipoProcessoCodigo == TodosOsTiposDeProcesso.Codigo
                    || o.TipoProcessoCodigo == tipoProcessoCodigo)
                && o.VigenciaInicio <= dataReferencia
                && (o.VigenciaFim == null || o.VigenciaFim > dataReferencia))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> ExisteRegraCodigoAtivoAsync(
        string regraCodigo,
        Guid? excluirId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(regraCodigo);

        IQueryable<ObrigatoriedadeLegal> query = _context.ObrigatoriedadesLegais
            .AsNoTracking()
            .Where(o => o.RegraCodigo == regraCodigo);

        if (excluirId is { } id)
        {
            query = query.Where(o => o.Id != id);
        }

        return await query.AnyAsync(cancellationToken).ConfigureAwait(false);
    }
}
