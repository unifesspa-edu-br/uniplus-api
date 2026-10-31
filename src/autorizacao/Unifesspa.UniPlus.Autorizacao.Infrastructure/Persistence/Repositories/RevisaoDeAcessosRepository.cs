namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>Repositório das revisões periódicas dos acessos.</summary>
public sealed class RevisaoDeAcessosRepository : IRevisaoDeAcessosRepository
{
    private readonly AutorizacaoDbContext _context;

    public RevisaoDeAcessosRepository(AutorizacaoDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public async Task<RevisaoDeAcessos?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _context.Revisoes.FirstOrDefaultAsync(r => r.Id == id, cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<RevisaoDeAcessos>> ObterTodosAsync(CancellationToken cancellationToken = default) =>
        await _context.Revisoes.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task AdicionarAsync(RevisaoDeAcessos entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        await _context.Revisoes.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public void Atualizar(RevisaoDeAcessos entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        // A revisão decide por item, e o item rastreado já entra na gravação com o xmin lido.
        _context.Entry(entity).State = EntityState.Modified;
    }

    public void Remover(RevisaoDeAcessos entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        // A revisão é registro de auditoria: encerra-se pelas decisões, nunca se exclui.
        throw new NotSupportedException("Revisão de acessos não é removida — ela se encerra pelas decisões dos itens.");
    }

    public async Task<RevisaoDeAcessos?> ObterParaAlterarAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _context.Revisoes
            .Include(r => r.Itens)
            .AsSplitQuery()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            .ConfigureAwait(false);

    public async Task<RevisaoDetalhada?> ObterDetalhadaAsync(Guid id, DateTimeOffset agora, CancellationToken cancellationToken = default)
    {
        RevisaoDeAcessos? revisao = await _context.Revisoes
            .AsNoTracking()
            .Include(r => r.Itens)
            .AsSplitQuery()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (revisao is null)
        {
            return null;
        }

        Guid[] concessaoIds = [.. revisao.Itens.Select(i => i.ConcessaoId)];
        var concessoes = await (
                from c in _context.Concessoes.AsNoTracking()
                join p in _context.Perfis.AsNoTracking() on c.PerfilId equals p.Id
                where concessaoIds.Contains(c.Id)
                select new { Concessao = c, PerfilNome = p.Nome })
            .ToDictionaryAsync(x => x.Concessao.Id, cancellationToken)
            .ConfigureAwait(false);

        // O perfil e a concessão existem sempre, pelas chaves estrangeiras.
        IReadOnlyList<ItemDaRevisaoComConcessao> itens =
        [
            .. revisao.Itens
                .OrderBy(i => i.Id)
                .Select(i => new ItemDaRevisaoComConcessao(
                    i,
                    concessoes[i.ConcessaoId].Concessao,
                    concessoes[i.ConcessaoId].PerfilNome,
                    concessoes[i.ConcessaoId].Concessao.EstaAtiva(agora))),
        ];

        return new RevisaoDetalhada(revisao, itens);
    }

    public async Task<(IReadOnlyList<ResumoDaRevisao> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        DateTimeOffset agora,
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken = default)
    {
        // Sem OrderBy aqui: o helper keyset (ADR-0089) ordena e fatia.
        CursorKeysetPage<RevisaoDeAcessos> page = await CursorKeyset
            .ApplyAsync(_context.Revisoes.AsNoTracking(), afterId, limit, direction, cancellationToken)
            .ConfigureAwait(false);

        Guid[] ids = [.. page.Items.Select(r => r.Id)];
        var contagens = await (
                from i in _context.ItensDasRevisoes.AsNoTracking()
                join c in _context.Concessoes.AsNoTracking() on i.ConcessaoId equals c.Id
                where ids.Contains(i.RevisaoId)
                group new { i, c } by i.RevisaoId into g
                select new
                {
                    RevisaoId = g.Key,
                    Total = g.Count(),
                    Pendentes = g.Count(x => x.i.Resultado == null
                        && x.c.RevogadaEm == null
                        && (x.c.ValidaAte == null || x.c.ValidaAte > agora)),
                })
            .ToDictionaryAsync(x => x.RevisaoId, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<ResumoDaRevisao> resumos =
        [
            .. page.Items.Select(r => contagens.TryGetValue(r.Id, out var c)
                ? new ResumoDaRevisao(r, c.Total, c.Pendentes)
                : new ResumoDaRevisao(r, 0, 0)),
        ];

        return (resumos, page.PrevAfterId, page.NextAfterId);
    }

    public async Task<bool> ExisteEmAndamentoAsync(DateTimeOffset agora, CancellationToken cancellationToken = default) =>
        await (
                from i in _context.ItensDasRevisoes.AsNoTracking()
                join c in _context.Concessoes.AsNoTracking() on i.ConcessaoId equals c.Id
                where i.Resultado == null && c.RevogadaEm == null && (c.ValidaAte == null || c.ValidaAte > agora)
                select i.Id)
            .AnyAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task TravarAberturaAsync(CancellationToken cancellationToken = default) =>
        _context.Database.ExecuteSqlRawAsync(
            "SELECT pg_advisory_xact_lock(hashtext('autorizacao.abertura_de_revisao'));",
            cancellationToken);

    public async Task<IReadOnlyList<Perfil>> ListarPerfisComConcessaoAtivaAsync(
        DateTimeOffset agora, CancellationToken cancellationToken = default) =>
        await _context.Perfis
            .AsNoTracking()
            .Where(p => _context.Concessoes.Any(c => c.PerfilId == p.Id
                && c.RevogadaEm == null
                && (c.ValidaAte == null || c.ValidaAte > agora)))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<(Guid ConcessaoId, Guid PerfilId, Identidade Beneficiario)>> ListarConcessoesAtivasDosPerfisAsync(
        IReadOnlyCollection<Guid> perfilIds, DateTimeOffset agora, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(perfilIds);

        var concessoes = await _context.Concessoes
            .AsNoTracking()
            .Where(c => perfilIds.Contains(c.PerfilId)
                && c.RevogadaEm == null
                && (c.ValidaAte == null || c.ValidaAte > agora))
            .OrderBy(c => c.Id)
            .Select(c => new { ConcessaoId = c.Id, c.PerfilId, c.BeneficiarioEmissor, c.BeneficiarioSub })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. concessoes.Select(c => (c.ConcessaoId, c.PerfilId, new Identidade(c.BeneficiarioEmissor, c.BeneficiarioSub)))];
    }

    public async Task<IReadOnlyList<ItemDaRevisao>> ListarDecisoesDaConcessaoAsync(
        Guid concessaoId, CancellationToken cancellationToken = default) =>
        await _context.ItensDasRevisoes
            .AsNoTracking()
            .Where(i => i.ConcessaoId == concessaoId && i.Resultado != null)
            .OrderBy(i => i.DecididoEm)
            .ThenBy(i => i.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}
