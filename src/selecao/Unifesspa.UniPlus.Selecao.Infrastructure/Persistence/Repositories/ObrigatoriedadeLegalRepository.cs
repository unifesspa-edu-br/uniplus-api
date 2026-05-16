namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using System.Collections.Generic;
using System.Linq;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Repositório de <see cref="ObrigatoriedadeLegal"/> + reconciliação atômica
/// da junction <c>obrigatoriedade_legal_areas_de_interesse</c> (ADR-0060).
/// </summary>
/// <remarks>
/// Reconciliação temporal (<see cref="ReconciliarBindingsAsync"/>): bindings
/// vigentes (<c>ValidoAte IS NULL</c>) cujo <c>AreaCodigo</c> some do novo
/// set são encerrados com <c>ValidoAte = agora</c>; bindings novos são
/// inseridos com <c>ValidoDe = agora</c>; bindings já vigentes que
/// permanecem no novo set ficam intactos. Tudo dentro do mesmo
/// <c>SaveChangesAsync</c> — atomicidade write+histórico via
/// <c>ObrigatoriedadeLegalHistoricoInterceptor</c>.
/// </remarks>
public sealed class ObrigatoriedadeLegalRepository : IObrigatoriedadeLegalRepository
{
    private readonly SelecaoDbContext _context;

    public ObrigatoriedadeLegalRepository(SelecaoDbContext context)
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

    public async Task<IReadOnlyList<ObrigatoriedadeLegal>> ListarPaginadoAsync(
        Guid? afterId,
        int take,
        string? tipoEditalCodigo,
        CategoriaObrigatoriedade? categoria,
        AreaCodigo? proprietario,
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

        if (proprietario is { } prop)
        {
            query = query.Where(o => o.Proprietario == prop);
        }

        if (vigentes)
        {
            DateOnly hoje = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime.Date);
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

    public async Task AdicionarComBindingsAsync(
        ObrigatoriedadeLegal regra,
        IReadOnlySet<AreaCodigo> areasDeInteresse,
        DateTimeOffset agora,
        string adicionadoPor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(regra);
        ArgumentNullException.ThrowIfNull(areasDeInteresse);
        ArgumentException.ThrowIfNullOrWhiteSpace(adicionadoPor);

        await _context.ObrigatoriedadesLegais.AddAsync(regra, cancellationToken).ConfigureAwait(false);

        DbSet<AreaDeInteresseBinding<ObrigatoriedadeLegal>> junction =
            _context.Set<AreaDeInteresseBinding<ObrigatoriedadeLegal>>();

        foreach (AreaCodigo area in areasDeInteresse)
        {
            await junction.AddAsync(
                AreaDeInteresseBinding<ObrigatoriedadeLegal>.Criar(regra.Id, area, agora, adicionadoPor),
                cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task ReconciliarBindingsAsync(
        ObrigatoriedadeLegal regra,
        IReadOnlySet<AreaCodigo> novasAreasDeInteresse,
        DateTimeOffset agora,
        string adicionadoPor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(regra);
        ArgumentNullException.ThrowIfNull(novasAreasDeInteresse);
        ArgumentException.ThrowIfNullOrWhiteSpace(adicionadoPor);

        DbSet<AreaDeInteresseBinding<ObrigatoriedadeLegal>> junction =
            _context.Set<AreaDeInteresseBinding<ObrigatoriedadeLegal>>();

        List<AreaDeInteresseBinding<ObrigatoriedadeLegal>> vigentes = await junction
            .Where(b => b.ParentId == regra.Id && b.ValidoAte == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        HashSet<AreaCodigo> vigentesCodigos = [.. vigentes.Select(b => b.AreaCodigo)];

        // Encerra bindings que sumiram do novo set — filtro explícito com
        // Where torna a intenção visível na origem da iteração.
        foreach (AreaDeInteresseBinding<ObrigatoriedadeLegal> binding in
            vigentes.Where(b => !novasAreasDeInteresse.Contains(b.AreaCodigo)))
        {
            binding.Encerrar(agora);
        }

        // Insere bindings que não existiam na junção. Strict greater-than no
        // ValidoDe versus o ValidoAte recém-fechado preserva o invariante de
        // janela do exclusion GIST.
        DateTimeOffset proximoValidoDe = agora.AddTicks(1);
        foreach (AreaCodigo nova in novasAreasDeInteresse.Where(a => !vigentesCodigos.Contains(a)))
        {
            await junction.AddAsync(
                AreaDeInteresseBinding<ObrigatoriedadeLegal>.Criar(
                    regra.Id, nova, proximoValidoDe, adicionadoPor),
                cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<IReadOnlySet<AreaCodigo>> ObterAreasVigentesAsync(
        Guid regraId,
        CancellationToken cancellationToken = default)
    {
        List<AreaCodigo> codigos = await _context
            .Set<AreaDeInteresseBinding<ObrigatoriedadeLegal>>()
            .AsNoTracking()
            .Where(b => b.ParentId == regraId && b.ValidoAte == null)
            .Select(b => b.AreaCodigo)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return codigos.ToHashSet();
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlySet<AreaCodigo>>> ObterAreasVigentesPorIdsAsync(
        IReadOnlyCollection<Guid> regraIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(regraIds);

        if (regraIds.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlySet<AreaCodigo>>();
        }

        var bindings = await _context
            .Set<AreaDeInteresseBinding<ObrigatoriedadeLegal>>()
            .AsNoTracking()
            .Where(b => regraIds.Contains(b.ParentId) && b.ValidoAte == null)
            .Select(b => new { b.ParentId, b.AreaCodigo })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Dictionary<Guid, IReadOnlySet<AreaCodigo>> map = bindings
            .GroupBy(b => b.ParentId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlySet<AreaCodigo>)g.Select(x => x.AreaCodigo).ToHashSet());

        return map;
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
