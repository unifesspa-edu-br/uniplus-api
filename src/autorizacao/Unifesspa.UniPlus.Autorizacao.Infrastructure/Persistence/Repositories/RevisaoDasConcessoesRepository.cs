namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

/// <summary>Repositório das revisões das concessões.</summary>
public sealed class RevisaoDasConcessoesRepository : IRevisaoDasConcessoesRepository
{
    private readonly UniPlusDbContext _context;

    public RevisaoDasConcessoesRepository(UniPlusDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public async Task AdicionarAsync(RevisaoDasConcessoesDoPerfil revisao, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(revisao);
        await _context.RevisoesDasConcessoes.AddAsync(revisao, cancellationToken).ConfigureAwait(false);
    }

    public async Task<RevisaoDasConcessoesDoPerfil?> ObterNaoEncerradaParaAlterarAsync(Guid perfilId, CancellationToken cancellationToken = default) =>
        await ComFilhas(_context.RevisoesDasConcessoes)
            .FirstOrDefaultAsync(j => j.PerfilId == perfilId && j.EncerradaEm == null, cancellationToken)
            .ConfigureAwait(false);

    public async Task<RevisaoDasConcessoesDoPerfil?> ObterUltimaParaLeituraAsync(Guid perfilId, CancellationToken cancellationToken = default) =>
        await ComFilhas(_context.RevisoesDasConcessoes.AsNoTracking())
            .Where(j => j.PerfilId == perfilId)
            .OrderByDescending(j => j.AbertaEm)
            .ThenByDescending(j => j.OrdemDeRegistro)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<bool> TemRevisaoAbertaAsync(Guid perfilId, DateTimeOffset agora, CancellationToken cancellationToken = default) =>
        await RevisoesEfetivamenteAbertas(perfilId, agora).AnyAsync(cancellationToken).ConfigureAwait(false);

    public async Task<bool> TemRevisaoPorEncerrarAsync(Guid perfilId, DateTimeOffset agora, CancellationToken cancellationToken = default)
    {
        IQueryable<RevisaoDasConcessoesDoPerfil> naoEncerradas = _context.RevisoesDasConcessoes.AsNoTracking()
            .Where(j => j.PerfilId == perfilId && j.EncerradaEm == null);

        return await naoEncerradas.AnyAsync(cancellationToken).ConfigureAwait(false)
            && !await RevisoesEfetivamenteAbertas(perfilId, agora).AnyAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Concessao>> ObterConcessoesDaRevisaoAsync(
        Guid perfilId, DateTimeOffset desde, CancellationToken cancellationToken = default)
    {
        await _context.Database.ExecuteSqlRawAsync(
            $"SELECT 1 FROM {AutorizacaoContribuicao.NomeDoEsquema}.concessoes WHERE perfil_id = {{0}} AND (revogada_em IS NULL OR revogada_em >= {{1}}) ORDER BY id FOR UPDATE",
            [perfilId, desde],
            cancellationToken).ConfigureAwait(false);

        return await _context.Concessoes
            .Include(c => c.Bases)
            .Where(c => c.PerfilId == perfilId && (c.RevogadaEm == null || c.RevogadaEm >= desde))
            .OrderBy(c => c.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Concessao>> ListarConcessoesDaRevisaoAsync(
        Guid perfilId, DateTimeOffset desde, CancellationToken cancellationToken = default) =>
        await _context.Concessoes.AsNoTracking()
            .Where(c => c.PerfilId == perfilId && (c.RevogadaEm == null || c.RevogadaEm >= desde))
            .OrderBy(c => c.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> ObterComposicoesAsync(
        IReadOnlyCollection<Guid> concessaoIds, DateTimeOffset agora, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(concessaoIds);

        IQueryable<Concessao> doConjunto = _context.Concessoes.AsNoTracking().Where(c => concessaoIds.Contains(c.Id));
        var pares = await _context.ComCodigosEmVigor(doConjunto, agora)
            .Select(l => new { l.Concessao.Id, l.Codigo })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return pares
            .GroupBy(static p => p.Id)
            .ToDictionary(static g => g.Key, static g => (IReadOnlyList<string>)[.. g.Select(static p => p.Codigo).OfType<CodigoDePermissao>().Select(static c => c.Valor).Order(StringComparer.Ordinal)]);
    }

    public async Task<IReadOnlyList<RevisaoDasConcessoesDoPerfil>> ListarAbertasParaLeituraAsync(
        DateTimeOffset agora, CancellationToken cancellationToken = default) =>
        await ComFilhas(_context.RevisoesDasConcessoes.AsNoTracking())
            .Where(j => j.EncerradaEm == null && j.TerminaEm > agora)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<Concessao>> ListarConcessoesAtivasDosPerfisAsync(
        IReadOnlyCollection<Guid> perfilIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(perfilIds);

        return await _context.Concessoes.AsNoTracking()
            .Where(c => perfilIds.Contains(c.PerfilId) && c.RevogadaEm == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Guid>> ListarPerfisComRevisaoNaoEncerradaAsync(CancellationToken cancellationToken = default) =>
        await _context.RevisoesDasConcessoes.AsNoTracking()
            .Where(j => j.EncerradaEm == null)
            .Select(j => j.PerfilId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<DecisaoDaMigracao>> ListarDecisoesDaConcessaoAsync(
        Guid concessaoId, CancellationToken cancellationToken = default) =>
        await _context.DecisoesDaMigracao.AsNoTracking()
            .Where(d => d.ConcessaoId == concessaoId)
            .OrderBy(d => d.Instante).ThenBy(d => d.OrdemDeRegistro)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    private IQueryable<RevisaoDasConcessoesDoPerfil> RevisoesEfetivamenteAbertas(Guid perfilId, DateTimeOffset agora) =>
        _context.EfetivamenteAbertas(agora).Where(j => j.PerfilId == perfilId);

    private static IQueryable<RevisaoDasConcessoesDoPerfil> ComFilhas(IQueryable<RevisaoDasConcessoesDoPerfil> revisoes) =>
        revisoes.Include(j => j.Envios).Include(j => j.Migracoes).Include(j => j.Decisoes).AsSplitQuery();
}
