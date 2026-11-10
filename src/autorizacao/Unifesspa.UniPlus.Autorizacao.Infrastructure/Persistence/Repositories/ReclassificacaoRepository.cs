namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>Repositório do catálogo gravado e das reclassificações das permissões.</summary>
public sealed class ReclassificacaoRepository : IReclassificacaoRepository
{
    private readonly AutorizacaoDbContext _context;

    public ReclassificacaoRepository(AutorizacaoDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public async Task<string?> ObterImpressaoAtualAsync(CancellationToken cancellationToken = default) =>
        await _context.ImplantacoesDasPermissoes.AsNoTracking()
            .OrderByDescending(c => c.Sequencia)
            .Select(c => c.Impressao)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<ImplantacaoNoBanco?> ObterImplantacaoAtualAsync(
        IReadOnlyCollection<string> codigos, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(codigos);

        ImplantacaoDasPermissoes? atual = await _context.ImplantacoesDasPermissoes.AsNoTracking()
            .OrderByDescending(c => c.Sequencia)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return atual is null
            ? null
            : new ImplantacaoNoBanco(atual.Id, atual.Impressao, await LinhasAsync(atual.Id, codigos, cancellationToken).ConfigureAwait(false));
    }

    private async Task<Dictionary<string, LinhaDoRetratoDoCatalogo>> LinhasAsync(
        Guid implantacaoId, IReadOnlyCollection<string> codigos, CancellationToken cancellationToken)
    {
        List<PermissaoImplantada> linhas = await _context.PermissoesImplantadas.AsNoTracking()
            .Where(p => p.ImplantacaoId == implantacaoId && codigos.Contains(p.Codigo))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return linhas.ToDictionary(
            static p => p.Codigo,
            static p => new LinhaDoRetratoDoCatalogo(p.Codigo, p.Modulo, p.Classificacao, p.DeUnidade, p.Reservada),
            StringComparer.Ordinal);
    }

    public async Task<IReadOnlySet<string>> ObterCodigosReclassificadosAsync(
        Guid implantacaoId, Guid perfilId, CancellationToken cancellationToken = default) =>
        (await _context.Reclassificacoes.AsNoTracking()
            .Where(r => r.ImplantacaoId == implantacaoId && r.PerfilId == perfilId)
            .Select(r => r.Codigo)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false)).ToHashSet(StringComparer.Ordinal);

    public void Adicionar(ReclassificacaoDaPermissao reclassificacao)
    {
        ArgumentNullException.ThrowIfNull(reclassificacao);
        _context.Reclassificacoes.Add(reclassificacao);
    }

    public async Task<Guid?> ObterPerfilIdAsync(Guid reclassificacaoId, CancellationToken cancellationToken = default) =>
        await _context.Reclassificacoes.AsNoTracking()
            .Where(r => r.Id == reclassificacaoId)
            .Select(r => (Guid?)r.PerfilId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<ReclassificacaoDaPermissao?> ObterAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _context.Reclassificacoes.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<ReclassificacaoDaPermissao>> ListarMaisRecentesQueSuspendemAsync(CancellationToken cancellationToken = default) =>
        await _context.Reclassificacoes.AsNoTracking()
            .Where(r => !_context.Reclassificacoes.Any(o => o.PerfilId == r.PerfilId && o.Codigo == r.Codigo && o.OrdemNaTrilha > r.OrdemNaTrilha))
            .OrderBy(r => r.OrdemNaTrilha)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<bool> EhMaisRecenteAsync(ReclassificacaoDaPermissao reclassificacao, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reclassificacao);

        return !await _context.Reclassificacoes.AsNoTracking()
            .AnyAsync(
                o => o.PerfilId == reclassificacao.PerfilId && o.Codigo == reclassificacao.Codigo && o.OrdemNaTrilha > reclassificacao.OrdemNaTrilha,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<LiberacaoDaReclassificacao>> ListarLiberacoesAsync(
        Guid reclassificacaoId, CancellationToken cancellationToken = default) =>
        await _context.LiberacoesDasReclassificacoes.AsNoTracking()
            .Where(l => l.ReclassificacaoId == reclassificacaoId)
            .OrderBy(l => l.OrdemNaTrilha)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<ConcessaoComAPermissao>> ListarConcessoesComAPermissaoAsync(
        Guid perfilId, string codigo, DateTimeOffset agora, bool travar, CancellationToken cancellationToken = default)
    {
        List<Guid> ids = await IdsDasConcessoesAsync(perfilId, codigo, agora, cancellationToken).ConfigureAwait(false);
        if (travar && ids.Count > 0)
        {
            await TravarParaLeituraAsync(ids, cancellationToken).ConfigureAwait(false);

            // Quem revogou enquanto a trava esperava já confirmou: o conjunto vale o de depois das travas.
            ids = await IdsDasConcessoesAsync(perfilId, codigo, agora, cancellationToken).ConfigureAwait(false);
        }

        return await ComAsBasesAsync(codigo, ids, agora, perfilId, cancellationToken).ConfigureAwait(false);
    }

    private Task<List<Guid>> IdsDasConcessoesAsync(Guid perfilId, string codigo, DateTimeOffset agora, CancellationToken cancellationToken) =>
        _context.CodigosDasConcessoesDoPerfil(perfilId, agora)
            .Where(k => k.Codigo == codigo).Select(k => k.ConcessaoId).OrderBy(id => id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Guid>> ListarVersoesEmUsoAsync(Guid perfilId, CancellationToken cancellationToken = default) =>
        await _context.VersoesEmUso(perfilId).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlySet<Guid>> ListarVersoesComDocumentoLidoAsync(
        Identidade leitor, IReadOnlyCollection<VersaoDoPerfil> versoes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(leitor);
        ArgumentNullException.ThrowIfNull(versoes);

        Guid[] portadoras = [.. versoes.Select(static v => v.VersaoDoDocumento ?? v.Id).Distinct()];
        List<Guid> lidas = await _context.LeiturasDosDocumentosDasVersoes.AsNoTracking()
            .Where(l => l.LeitorEmissor == leitor.Emissor && l.LeitorSub == leitor.Sub)
            .Join(_context.DocumentosDasVersoes, l => l.DocumentoId, d => d.Id, (l, d) => d.VersaoId)
            .Where(versaoId => portadoras.Contains(versaoId))
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return versoes.Where(v => lidas.Contains(v.VersaoDoDocumento ?? v.Id)).Select(static v => v.Id).ToHashSet();
    }

    public async Task<IReadOnlyList<DivergenciaDaGovernanca>> ListarDivergenciasAsync(CancellationToken cancellationToken = default) =>
        await _context.DivergenciasDaGovernanca.AsNoTracking()
            .OrderByDescending(d => d.OrdemNaTrilha)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyDictionary<Guid, string>> ObterNomesDosPerfisAsync(
        IReadOnlyCollection<Guid> perfilIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(perfilIds);

        return await _context.Perfis.AsNoTracking()
            .Where(p => perfilIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Nome, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<FatoSemAviso>> ListarFatosSemAvisoAsync(int limite, CancellationToken cancellationToken = default)
    {
        IQueryable<FatoSemAviso> reclassificacoes = _context.Reclassificacoes.AsNoTracking()
            .Where(r => (FuncoesDoBanco.OrdemDaClassificacao(r.ClassificacaoNova) > FuncoesDoBanco.OrdemDaClassificacao(r.ClassificacaoAnterior) || r.DeUnidadeNovo != r.DeUnidadeAnterior)
                && !_context.AvisosDasReclassificacoes.Any(a => a.Origem == OrigemDoAviso.Reclassificacao && a.OrigemId == r.Id))
            .OrderBy(r => r.OrdemNaTrilha)
            .Take(limite)
            .Select(r => new FatoSemAviso(OrigemDoAviso.Reclassificacao, r.Id));
        IQueryable<FatoSemAviso> divergencias = _context.DivergenciasDaGovernanca.AsNoTracking()
            .Where(d => !_context.AvisosDasReclassificacoes.Any(a => a.Origem == OrigemDoAviso.Divergencia && a.OrigemId == d.Id))
            .OrderBy(d => d.OrdemNaTrilha)
            .Take(limite)
            .Select(d => new FatoSemAviso(OrigemDoAviso.Divergencia, d.Id));

        return [.. (await reclassificacoes.ToListAsync(cancellationToken).ConfigureAwait(false))
            .Concat(await divergencias.ToListAsync(cancellationToken).ConfigureAwait(false))
            .Take(limite)];
    }

    public async Task<DivergenciaDaGovernanca?> ObterDivergenciaAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _context.DivergenciasDaGovernanca.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, cancellationToken).ConfigureAwait(false);

    public void Adicionar(AvisoDaReclassificacao aviso)
    {
        ArgumentNullException.ThrowIfNull(aviso);
        _context.AvisosDasReclassificacoes.Add(aviso);
    }

    public void Adicionar(LiberacaoDaReclassificacao liberacao)
    {
        ArgumentNullException.ThrowIfNull(liberacao);
        _context.LiberacoesDasReclassificacoes.Add(liberacao);
    }

    public void Adicionar(BaseDaPermissaoNaConcessao baseNova)
    {
        ArgumentNullException.ThrowIfNull(baseNova);
        _context.BasesDasPermissoes.Add(baseNova);
    }

    private Task<int> TravarParaLeituraAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken) =>
        _context.Database.ExecuteSqlRawAsync(
            $"SELECT 1 FROM {AutorizacaoDbContext.Schema}.concessoes WHERE id = ANY({{0}}) ORDER BY id FOR SHARE",
            [ids.ToArray()],
            cancellationToken);

    private async Task<IReadOnlyList<ConcessaoComAPermissao>> ComAsBasesAsync(
        string codigo, List<Guid> ids, DateTimeOffset agora, Guid perfilId, CancellationToken cancellationToken)
    {
        List<Concessao> concessoes = await _context.Concessoes.AsNoTracking()
            .Where(c => ids.Contains(c.Id)).OrderBy(c => c.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
        Dictionary<Guid, CodigoDaConcessao> retratos = (await _context.CodigosDasConcessoesDoPerfil(perfilId, agora)
            .Where(k => k.Codigo == codigo).ToListAsync(cancellationToken).ConfigureAwait(false)).ToDictionary(static k => k.ConcessaoId);
        Dictionary<Guid, BaseViva> vivas = (await _context.BasesVivas().Where(b => b.Codigo == codigo && ids.Contains(b.ConcessaoId))
            .ToListAsync(cancellationToken).ConfigureAwait(false)).ToDictionary(static b => b.ConcessaoId);

        return
        [.. concessoes
            .Where(c => retratos.ContainsKey(c.Id))
            .Select(c => new ConcessaoComAPermissao(
                c,
                vivas.TryGetValue(c.Id, out BaseViva? viva)
                    ? new BaseDaConcessao(viva.Classificacao, viva.DeUnidade)
                    : new BaseDaConcessao(retratos[c.Id].RetratoDaClassificacao!.Value, retratos[c.Id].RetratoDeUnidade)))];
    }
}
