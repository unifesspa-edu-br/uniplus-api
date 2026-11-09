namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>Repositório das janelas de migração.</summary>
public sealed class JanelaDeMigracaoRepository : IJanelaDeMigracaoRepository
{
    private readonly AutorizacaoDbContext _context;

    public JanelaDeMigracaoRepository(AutorizacaoDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public async Task AdicionarAsync(JanelaDeMigracao janela, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(janela);
        await _context.JanelasDeMigracao.AddAsync(janela, cancellationToken).ConfigureAwait(false);
    }

    public async Task<JanelaDeMigracao?> ObterNaoEncerradaParaAlterarAsync(Guid perfilId, CancellationToken cancellationToken = default) =>
        await ComFilhas(_context.JanelasDeMigracao)
            .FirstOrDefaultAsync(j => j.PerfilId == perfilId && j.EncerradaEm == null, cancellationToken)
            .ConfigureAwait(false);

    public async Task<JanelaDeMigracao?> ObterUltimaParaLeituraAsync(Guid perfilId, CancellationToken cancellationToken = default) =>
        await ComFilhas(_context.JanelasDeMigracao.AsNoTracking())
            .Where(j => j.PerfilId == perfilId)
            .OrderByDescending(j => j.AbertaEm)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<bool> TemJanelaAbertaAsync(Guid perfilId, DateTimeOffset agora, CancellationToken cancellationToken = default) =>
        await JanelasEfetivamenteAbertas(perfilId, agora).AnyAsync(cancellationToken).ConfigureAwait(false);

    public async Task<bool> TemJanelaPorMaterializarAsync(Guid perfilId, DateTimeOffset agora, CancellationToken cancellationToken = default)
    {
        IQueryable<JanelaDeMigracao> naoEncerradas = _context.JanelasDeMigracao.AsNoTracking()
            .Where(j => j.PerfilId == perfilId && j.EncerradaEm == null);

        return await naoEncerradas.AnyAsync(cancellationToken).ConfigureAwait(false)
            && !await JanelasEfetivamenteAbertas(perfilId, agora).AnyAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Concessao>> ObterConcessoesDaJanelaAsync(
        Guid perfilId, DateTimeOffset desde, CancellationToken cancellationToken = default)
    {
        await _context.Database.ExecuteSqlRawAsync(
            $"SELECT 1 FROM {AutorizacaoDbContext.Schema}.concessoes WHERE perfil_id = {{0}} AND (revogada_em IS NULL OR revogada_em >= {{1}}) ORDER BY id FOR UPDATE",
            [perfilId, desde],
            cancellationToken).ConfigureAwait(false);

        return await _context.Concessoes
            .Where(c => c.PerfilId == perfilId && (c.RevogadaEm == null || c.RevogadaEm >= desde))
            .OrderBy(c => c.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<Identidade>> TravarTitularesDaProtecaoDeDadosAsync(
        DateTimeOffset agora, CancellationToken cancellationToken = default)
    {
        Guid perfilId = PerfisDeSistema.ProtecaoDeDadosId;
        await _context.Database.ExecuteSqlRawAsync(
            $"SELECT 1 FROM {AutorizacaoDbContext.Schema}.perfis WHERE id = {{0}} FOR UPDATE",
            [perfilId],
            cancellationToken).ConfigureAwait(false);
        await _context.Database.ExecuteSqlRawAsync(
            $"SELECT 1 FROM {AutorizacaoDbContext.Schema}.concessoes WHERE perfil_id = {{0}} AND revogada_em IS NULL ORDER BY id FOR UPDATE",
            [perfilId],
            cancellationToken).ConfigureAwait(false);

        var titulares = await _context.Concessoes.AsNoTracking()
            .Where(c => c.PerfilId == perfilId && c.RevogadaEm == null && (c.ValidaAte == null || c.ValidaAte > agora))
            .Select(c => new { c.BeneficiarioEmissor, c.BeneficiarioSub })
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. titulares.Select(static t => new Identidade(t.BeneficiarioEmissor, t.BeneficiarioSub))];
    }

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
            .ToDictionary(static g => g.Key, static g => (IReadOnlyList<string>)[.. g.Select(static p => p.Codigo).OfType<string>().Order(StringComparer.Ordinal)]);
    }

    public async Task<IReadOnlyList<Guid>> ListarPerfisComJanelaNaoEncerradaAsync(CancellationToken cancellationToken = default) =>
        await _context.JanelasDeMigracao.AsNoTracking()
            .Where(j => j.EncerradaEm == null)
            .Select(j => j.PerfilId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<DecisaoDaMigracao>> ListarDecisoesDaConcessaoAsync(
        Guid concessaoId, CancellationToken cancellationToken = default) =>
        await _context.DecisoesDaMigracao.AsNoTracking()
            .Where(d => d.ConcessaoId == concessaoId)
            .OrderBy(d => d.Instante).ThenBy(d => d.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    // Não encerrada, com o prazo por vencer e ao menos uma concessão elegível: ainda não revogada, não vencida e fora da versão-alvo.
    private IQueryable<JanelaDeMigracao> JanelasEfetivamenteAbertas(Guid perfilId, DateTimeOffset agora) =>
        _context.JanelasDeMigracao.AsNoTracking()
            .Where(j => j.PerfilId == perfilId && j.EncerradaEm == null && j.Prazo > agora
                && _context.Concessoes.Any(c => c.PerfilId == j.PerfilId && c.RevogadaEm == null
                    && (c.ValidaAte == null || c.ValidaAte > agora) && c.VersaoId != j.VersaoAlvoId));

    private static IQueryable<JanelaDeMigracao> ComFilhas(IQueryable<JanelaDeMigracao> janelas) =>
        janelas.Include(j => j.Lotes).Include(j => j.Migracoes).Include(j => j.Decisoes).AsSplitQuery();
}
