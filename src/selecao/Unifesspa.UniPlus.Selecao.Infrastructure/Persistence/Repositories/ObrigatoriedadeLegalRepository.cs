namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using System.Collections.Generic;
using System.Linq;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Repositório de <see cref="ObrigatoriedadeLegal"/>.
/// </summary>
/// <remarks>
/// Escritas (Adicionar/Atualizar/Remover) e leituras (listagem paginada,
/// vigentes para tipo de edital, colisão de código) ocorrem dentro do
/// <c>SaveChangesAsync</c> do contexto — a captura de histórico append-only
/// é responsabilidade do <c>ObrigatoriedadeLegalHistoricoInterceptor</c>.
/// </remarks>
public sealed class ObrigatoriedadeLegalRepository : IObrigatoriedadeLegalRepository
{
    private readonly SelecaoDbContext _context;
    private readonly TimeProvider _timeProvider;

    public ObrigatoriedadeLegalRepository(SelecaoDbContext context, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _context = context;
        _timeProvider = timeProvider;
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

    public async Task<IReadOnlyList<ObrigatoriedadeLegal>> ListarPaginadoAsync(
        Guid? afterId,
        int take,
        string? tipoEditalCodigo,
        CategoriaObrigatoriedade? categoria,
        bool vigentes,
        CancellationToken cancellationToken = default)
    {
        IQueryable<ObrigatoriedadeLegal> query = _context.ObrigatoriedadesLegais
            .AsNoTracking()
            .OrderBy(o => o.Id);

        if (!string.IsNullOrWhiteSpace(tipoEditalCodigo))
        {
            string normalizado = tipoEditalCodigo.Trim();
            query = query.Where(o => o.TipoEditalCodigo == normalizado);
        }

        if (categoria is { } cat)
        {
            query = query.Where(o => o.Categoria == cat);
        }

        if (vigentes)
        {
            DateOnly hoje = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime.Date);
            query = query.Where(o =>
                o.VigenciaInicio <= hoje
                && (o.VigenciaFim == null || o.VigenciaFim > hoje));
        }

        if (afterId is { } cursor)
        {
            query = query.Where(o => o.Id.CompareTo(cursor) > 0);
        }

        return await query.Take(take).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ObrigatoriedadeLegal>> ObterVigentesParaTipoEditalAsync(
        string tipoEditalCodigo,
        DateOnly hoje,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tipoEditalCodigo);

        // Comparação case-insensitive defensiva (Codex P2 round 2): mesmo
        // que o caller já passe o lookup uppercased, a persistência aceita
        // caixa livre — divergências de caixa entre Criar/Atualizar e o
        // lookup quebrariam silenciosamente a conformidade. `EF.Functions.ILike`
        // traduz para o operador case-insensitive nativo do PostgreSQL.
        // Universal "*" segue por igualdade direta — sentinela ASCII sem
        // letras.
        return await _context.ObrigatoriedadesLegais
            .AsNoTracking()
            .Where(o =>
                (o.TipoEditalCodigo == ObrigatoriedadeLegal.TipoEditalUniversal
                    || EF.Functions.ILike(o.TipoEditalCodigo, tipoEditalCodigo))
                && o.VigenciaInicio <= hoje
                && (o.VigenciaFim == null || o.VigenciaFim > hoje))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> ExisteRegraCodigoAtivoAsync(
        string regraCodigo,
        Guid? excluirId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(regraCodigo);

        string normalizado = regraCodigo.Trim();
        IQueryable<ObrigatoriedadeLegal> query = _context.ObrigatoriedadesLegais
            .AsNoTracking()
            .Where(o => o.RegraCodigo == normalizado);

        if (excluirId is { } id)
        {
            query = query.Where(o => o.Id != id);
        }

        return await query.AnyAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<string?> ObterSnapshotConformidadeJsonAsync(
        Guid editalId,
        CancellationToken cancellationToken = default)
    {
        return await _context.EditalGovernanceSnapshots
            .AsNoTracking()
            .Where(s => s.EditalId == editalId)
            .OrderByDescending(s => s.SnapshottedAt)
            .Select(s => s.RegrasJson)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
