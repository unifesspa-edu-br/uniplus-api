namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Configurations;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>Repositório dos perfis de acesso.</summary>
public sealed class PerfilRepository : IPerfilRepository
{
    private readonly UniPlusDbContext _context;

    public PerfilRepository(UniPlusDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public async Task<Perfil?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _context.Perfis.ComComposicao().FirstOrDefaultAsync(p => p.Id == id, cancellationToken).ConfigureAwait(false);

    public async Task<(IReadOnlyList<Perfil> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPediveisPaginadoAsync(
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken = default)
    {
        // O filtro vem antes da paginação: o perfil comum sem versão em vigor não ocupa lugar na página.
        IQueryable<Perfil> pediveis = _context.Perfis
            .AsNoTracking()
            .ComComposicao()
            .Where(p => p.Ativo
                && (p.Governanca != null || _context.Set<VigenciaDoPerfil>().Any(v => v.PerfilId == p.Id && v.Ate == null)));

        CursorKeysetPage<Perfil> page = await CursorKeyset
            .ApplyAsync(pediveis, afterId, limit, direction, cancellationToken)
            .ConfigureAwait(false);

        return (page.Items, page.PrevAfterId, page.NextAfterId);
    }

    public async Task<IReadOnlyList<Perfil>> ListarComPendenteNoPrazoAsync(
        DateTimeOffset agora, CancellationToken cancellationToken = default) =>
        await _context.Perfis
            .AsNoTracking()
            .ComComposicao()
            .Where(p => p.Versoes.Any(v => v.Situacao == SituacaoDaVersao.Pendente && v.ExpiraEm > agora))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<Perfil>> ListarPorIdsAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        return await _context.Perfis.AsNoTracking().ComComposicao().Where(p => ids.Contains(p.Id)).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Guid>> ListarComPendenteVencidaAsync(
        DateTimeOffset agora, CancellationToken cancellationToken = default) =>
        await _context.Perfis
            .AsNoTracking()
            .Where(p => p.Versoes.Any(v => v.Situacao == SituacaoDaVersao.Pendente && v.ExpiraEm <= agora))
            .Select(p => p.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<Perfil?> ObterParaEditarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // A trava vem antes da leitura rastreada, que então enxerga o que a transação anterior confirmou.
        await _context.Database.ExecuteSqlRawAsync(
            $"SELECT 1 FROM {AutorizacaoContribuicao.NomeDoEsquema}.perfis WHERE id = {{0}} FOR UPDATE",
            [id],
            cancellationToken).ConfigureAwait(false);

        return await ObterPorIdAsync(id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Perfil>> ObterTodosAsync(CancellationToken cancellationToken = default) =>
        await _context.Perfis.AsNoTracking().ComComposicao().ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<Guid?> ObterPerfilIdDoDocumentoAsync(Guid documentoId, CancellationToken cancellationToken = default) =>
        await _context.DocumentosDasVersoes
            .AsNoTracking()
            .Where(d => d.Id == documentoId)
            .Select(d => (Guid?)d.PerfilId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task AdicionarLeituraAsync(LeituraDoDocumentoDaVersao leitura, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(leitura);
        await _context.LeiturasDosDocumentosDasVersoes.AddAsync(leitura, cancellationToken).ConfigureAwait(false);
    }

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
            .ApplyAsync(_context.Perfis.AsNoTracking().ComComposicao(), afterId, limit, direction, cancellationToken)
            .ConfigureAwait(false);

        return (page.Items, page.PrevAfterId, page.NextAfterId);
    }

    public async Task<TrilhaDaContencao?> ObterTrilhaDaContencaoAsync(Guid perfilId, CancellationToken cancellationToken = default)
    {
        if (!await _context.Perfis.AnyAsync(p => p.Id == perfilId, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new TrilhaDaContencao(
            await _context.EventosDaContencao.AsNoTracking().Where(e => e.PerfilId == perfilId).OrderBy(e => e.OrdemNaTrilha)
                .ToListAsync(cancellationToken).ConfigureAwait(false),
            await _context.DecisoesDasRetomadas.AsNoTracking()
                .Where(d => _context.Set<SuspensaoDoPerfil>().Any(s => s.Id == d.SuspensaoId && s.PerfilId == perfilId))
                .OrderBy(d => d.OrdemNaTrilha)
                .ToListAsync(cancellationToken).ConfigureAwait(false));
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
