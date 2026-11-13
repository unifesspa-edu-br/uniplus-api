namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Repositório das providências sobre concessões. As leituras são sem rastreamento, exceto a de quem altera: lida antes da
/// trava, a providência ou a concessão rastreada ficaria no mapa de identidade com os valores velhos, e a leitura depois
/// da trava os devolveria.
/// </summary>
public sealed class ProvidenciaRepository : IProvidenciaRepository
{
    private readonly AutorizacaoDbContext _context;

    public ProvidenciaRepository(AutorizacaoDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    // Rastreada, com o xmin lido: é a leitura de quem altera, depois da trava da concessão.
    public async Task<Providencia?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _context.Providencias.FirstOrDefaultAsync(p => p.Id == id, cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<Providencia>> ObterTodosAsync(CancellationToken cancellationToken = default) =>
        await _context.Providencias.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task AdicionarAsync(Providencia entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        await _context.Providencias.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public void Atualizar(Providencia entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        _context.Providencias.Update(entity);
    }

    public void Remover(Providencia entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        // A providência é registro de auditoria: encerra-se pelo desfecho, nunca se exclui.
        throw new NotSupportedException("Providência não é removida — ela se encerra pela resposta ou pelo atendimento.");
    }

    public async Task<Guid?> ObterConcessaoIdAsync(Guid providenciaId, CancellationToken cancellationToken = default) =>
        await _context.Providencias
            .AsNoTracking()
            .Where(p => p.Id == providenciaId)
            .Select(p => (Guid?)p.ConcessaoId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<ConcessaoDaProvidencia?> ObterConcessaoSobTravaCompartilhadaAsync(Guid concessaoId, CancellationToken cancellationToken = default)
    {
        // A trava vem antes da leitura, que então enxerga o que a revogação concorrente já confirmou.
        await _context.Database.ExecuteSqlRawAsync(
            $"SELECT 1 FROM {AutorizacaoDbContext.Schema}.concessoes WHERE id = {{0}} FOR SHARE",
            [concessaoId],
            cancellationToken).ConfigureAwait(false);

        return (await AlvosAsync([concessaoId], cancellationToken).ConfigureAwait(false)).GetValueOrDefault(concessaoId);
    }

    public async Task<Providencia?> ObterAbertaDaConcessaoAsync(Guid concessaoId, CancellationToken cancellationToken = default) =>
        await _context.Providencias
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.ConcessaoId == concessaoId && p.Estado == EstadoDaProvidencia.Aberta, cancellationToken)
            .ConfigureAwait(false);

    public async Task<ProvidenciaComConcessao?> ObterComConcessaoAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Providencia? providencia = await _context.Providencias.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken).ConfigureAwait(false);

        return providencia is null ? null : (await ComAlvosAsync([providencia], cancellationToken).ConfigureAwait(false))[0];
    }

    public async Task<(IReadOnlyList<ProvidenciaComConcessao> Itens, (string SortKey, Guid Id)? Anterior, (string SortKey, Guid Id)? Proximo)> ListarPaginadoAsync(
        FiltroDeProvidencias filtro,
        string? afterSortKey,
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        // Sem OrderBy aqui: o motor keyset ordena por (aberta_em, id) nos dois sentidos (ADR-0089).
        OrderedKeysetPage<Providencia> page = await OrderedKeysetCursor
            .ApplyAsync(NaSituacao(filtro), OrdenacaoDasProvidencias.Montar(filtro), afterSortKey, afterId, limit, direction, cancellationToken)
            .ConfigureAwait(false);

        return (await ComAlvosAsync(page.Items, cancellationToken).ConfigureAwait(false), page.Previous, page.Next);
    }

    public async Task<IReadOnlyList<Providencia>> ListarDaConcessaoAsync(Guid concessaoId, CancellationToken cancellationToken = default) =>
        await _context.Providencias
            .AsNoTracking()
            .Where(p => p.ConcessaoId == concessaoId)
            .OrderBy(p => p.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    // O critério de Concessao.EstaAtiva reproduzido no banco: a aberta com concessão encerrada é a prejudicada.
    private IQueryable<Providencia> NaSituacao(FiltroDeProvidencias filtro)
    {
        IQueryable<Providencia> providencias = DaConcessao(filtro.ConcessaoId);
        IQueryable<Guid> ativas = ConcessoesAtivas(filtro.Agora);

        return filtro.Situacao switch
        {
            SituacaoDaProvidencia.Aberta => providencias.Where(p => p.Estado == EstadoDaProvidencia.Aberta && ativas.Contains(p.ConcessaoId)),
            SituacaoDaProvidencia.Prejudicada => providencias.Where(p => p.Estado == EstadoDaProvidencia.Aberta && !ativas.Contains(p.ConcessaoId)),
            SituacaoDaProvidencia.Respondida => providencias.Where(p => p.Estado == EstadoDaProvidencia.Respondida),
            SituacaoDaProvidencia.Atendida => providencias.Where(p => p.Estado == EstadoDaProvidencia.Atendida),
            _ => providencias,
        };
    }

    private IQueryable<Providencia> DaConcessao(Guid? concessaoId) =>
        concessaoId is { } id
            ? _context.Providencias.AsNoTracking().Where(p => p.ConcessaoId == id)
            : _context.Providencias.AsNoTracking();

    private IQueryable<Guid> ConcessoesAtivas(DateTimeOffset agora) =>
        _context.Concessoes.AsNoTracking()
            .Where(c => c.RevogadaEm == null && (c.ValidaAte == null || c.ValidaAte > agora))
            .Select(c => c.Id);

    private async Task<IReadOnlyList<ProvidenciaComConcessao>> ComAlvosAsync(IReadOnlyList<Providencia> providencias, CancellationToken cancellationToken)
    {
        Dictionary<Guid, ConcessaoDaProvidencia> alvos = await AlvosAsync([.. providencias.Select(p => p.ConcessaoId).Distinct()], cancellationToken).ConfigureAwait(false);

        return [.. providencias.Select(p => new ProvidenciaComConcessao(p, alvos[p.ConcessaoId]))];
    }

    // A concessão e o perfil existem sempre, pelas chaves estrangeiras.
    private async Task<Dictionary<Guid, ConcessaoDaProvidencia>> AlvosAsync(Guid[] concessaoIds, CancellationToken cancellationToken)
    {
        var alvos = await (
                from c in _context.Concessoes.AsNoTracking()
                join p in _context.Perfis.AsNoTracking() on c.PerfilId equals p.Id
                where concessaoIds.Contains(c.Id)
                select new { Concessao = c, PerfilNome = p.Nome })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return alvos.ToDictionary(static a => a.Concessao.Id, static a => new ConcessaoDaProvidencia(a.Concessao, a.PerfilNome));
    }
}
