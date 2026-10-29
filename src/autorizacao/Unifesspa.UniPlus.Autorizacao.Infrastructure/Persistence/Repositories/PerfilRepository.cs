namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Configurations;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>Repositório dos perfis de acesso.</summary>
public sealed class PerfilRepository : IPerfilRepository
{
    private readonly AutorizacaoDbContext _context;

    public PerfilRepository(AutorizacaoDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public async Task<Perfil?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _context.Perfis.FirstOrDefaultAsync(p => p.Id == id, cancellationToken).ConfigureAwait(false);

    public async Task<(IReadOnlyList<Perfil> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarAtivosPaginadoAsync(
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken = default)
    {
        CursorKeysetPage<Perfil> page = await CursorKeyset
            .ApplyAsync(_context.Perfis.AsNoTracking().Where(p => p.Ativo), afterId, limit, direction, cancellationToken)
            .ConfigureAwait(false);

        return (page.Items, page.PrevAfterId, page.NextAfterId);
    }

    public async Task<Perfil?> ObterParaEditarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // A trava vem antes da leitura rastreada, que então enxerga o que a transação anterior confirmou.
        await _context.Database.ExecuteSqlRawAsync(
            $"SELECT 1 FROM {AutorizacaoDbContext.Schema}.perfis WHERE id = {{0}} FOR UPDATE",
            [id],
            cancellationToken).ConfigureAwait(false);

        return await ObterPorIdAsync(id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Perfil>> ObterTodosAsync(CancellationToken cancellationToken = default) =>
        await _context.Perfis.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task AdicionarAsync(Perfil entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        await _context.Perfis.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public void Atualizar(Perfil entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        _context.Perfis.Update(entity);
    }

    public void Remover(Perfil entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        // Perfil não se exclui: a retirada é a desativação, que preserva as concessões que
        // ainda o referenciam. O método existe porque IRepository o declara.
        throw new NotSupportedException("Perfil de acesso não é removido — a retirada é a desativação.");
    }

    public async Task<(IReadOnlyList<Perfil> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken = default)
    {
        // Sem OrderBy aqui: o helper keyset (ADR-0089) ordena e fatia.
        CursorKeysetPage<Perfil> page = await CursorKeyset
            .ApplyAsync(_context.Perfis.AsNoTracking(), afterId, limit, direction, cancellationToken)
            .ConfigureAwait(false);

        return (page.Items, page.PrevAfterId, page.NextAfterId);
    }

    public async Task<bool> NomeExisteAsync(string nome, Guid? exceto, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nome);

        string minusculo = nome.ToLowerInvariant();

        return await _context.Perfis
            .AsNoTracking()
            .AnyAsync(
                p => EF.Property<string>(p, PerfilConfiguration.NomeNormalizado) == minusculo && p.Id != exceto,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
