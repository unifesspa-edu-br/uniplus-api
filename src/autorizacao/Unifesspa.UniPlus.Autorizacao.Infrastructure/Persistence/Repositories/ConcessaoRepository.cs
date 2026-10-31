namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>Repositório das concessões de acesso.</summary>
public sealed class ConcessaoRepository : IConcessaoRepository
{
    private readonly AutorizacaoDbContext _context;

    public ConcessaoRepository(AutorizacaoDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public async Task<Concessao?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _context.Concessoes.FirstOrDefaultAsync(c => c.Id == id, cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<Concessao>> ObterTodosAsync(CancellationToken cancellationToken = default) =>
        await _context.Concessoes.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task AdicionarAsync(Concessao entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        await _context.Concessoes.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public void Atualizar(Concessao entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        _context.Concessoes.Update(entity);
    }

    public void Remover(Concessao entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        // Concessão não se exclui: a retirada é a revogação. O método existe porque
        // IRepository o declara.
        throw new NotSupportedException("Concessão de acesso não é removida — a retirada é a revogação.");
    }

    public async Task<Perfil?> ObterParaConcederAsync(Guid perfilId, CancellationToken cancellationToken = default)
    {
        // A trava vem antes da leitura: a leitura seguinte enxerga o que a transação que
        // desativava o perfil já confirmou. A trava dura até o fim da transação do handler.
        await _context.Database.ExecuteSqlRawAsync(
            $"SELECT 1 FROM {AutorizacaoDbContext.Schema}.perfis WHERE id = {{0}} FOR SHARE",
            [perfilId],
            cancellationToken).ConfigureAwait(false);

        return await _context.Perfis
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == perfilId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Concessao?> ObterNaoRevogadaIgualAsync(
        Identidade beneficiario, Guid perfilId, EscopoDaConcessao escopo, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(beneficiario);
        ArgumentNullException.ThrowIfNull(escopo);

        return await _context.Concessoes
            .Where(c => c.BeneficiarioEmissor == beneficiario.Emissor
                && c.BeneficiarioSub == beneficiario.Sub
                && c.PerfilId == perfilId
                && c.EscopoTipo == escopo.Tipo
                && c.UnidadeId == escopo.UnidadeId
                && c.RevogadaEm == null)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> TemConcessaoAtivaDoPerfilAsync(
        Identidade pessoa, Guid perfilId, DateTimeOffset agora, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pessoa);

        return await _context.Concessoes
            .AsNoTracking()
            .AnyAsync(
                c => c.BeneficiarioEmissor == pessoa.Emissor
                    && c.BeneficiarioSub == pessoa.Sub
                    && c.PerfilId == perfilId
                    && c.EscopoTipo == TipoDeEscopo.Instituicao
                    && c.RevogadaEm == null
                    && (c.ValidaAte == null || c.ValidaAte > agora),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ConcessaoComPerfil?> ObterComPerfilAsync(Guid id, CancellationToken cancellationToken = default) =>
        await ComPerfil(_context.Concessoes.AsNoTracking().Where(c => c.Id == id))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<(IReadOnlyList<ConcessaoComPerfil> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        FiltroDeConcessoes filtro,
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        IQueryable<Concessao> consulta = _context.Concessoes.AsNoTracking();

        if (filtro.PessoaEmissor is not null && filtro.PessoaSub is not null)
        {
            string emissor = filtro.PessoaEmissor;
            string sub = filtro.PessoaSub;
            consulta = consulta.Where(c => c.BeneficiarioEmissor == emissor && c.BeneficiarioSub == sub);
        }

        if (filtro.UnidadeId is { } unidadeId)
        {
            consulta = consulta.Where(c => c.UnidadeId == unidadeId);
        }

        if (filtro.PerfilId is { } perfilId)
        {
            consulta = consulta.Where(c => c.PerfilId == perfilId);
        }

        DateTimeOffset agora = filtro.Agora;
        consulta = filtro.Situacao switch
        {
            SituacaoDaConcessao.Ativa => consulta.Where(c => c.RevogadaEm == null && (c.ValidaAte == null || c.ValidaAte > agora)),
            SituacaoDaConcessao.Revogada => consulta.Where(c => c.RevogadaEm != null),
            SituacaoDaConcessao.Vencida => consulta.Where(c => c.RevogadaEm == null && c.ValidaAte != null && c.ValidaAte <= agora),
            _ => consulta,
        };

        // Sem OrderBy aqui: o helper keyset (ADR-0089) ordena e fatia.
        CursorKeysetPage<ConcessaoComPerfil> page = await CursorKeyset
            .ApplyAsync(ComPerfil(consulta), afterId, limit, direction, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<ConcessaoComPerfil> itens = page.Items;

        return (itens, page.PrevAfterId, page.NextAfterId);
    }

    public async Task<bool> ExisteNaoRevogadaDoPerfilAsync(Guid perfilId, CancellationToken cancellationToken = default) =>
        await _context.Concessoes
            .AsNoTracking()
            .AnyAsync(c => c.PerfilId == perfilId && c.RevogadaEm == null, cancellationToken)
            .ConfigureAwait(false);

    public async Task<TipoDeGovernanca?> ObterGovernancaDoPerfilAsync(Guid perfilId, CancellationToken cancellationToken = default) =>
        await _context.Perfis
            .AsNoTracking()
            .Where(p => p.Id == perfilId)
            .Select(p => p.Governanca)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task TravarGovernancaParaEscritaAsync(CancellationToken cancellationToken = default) =>
        _context.Database.ExecuteSqlRawAsync(
            "SELECT pg_advisory_xact_lock(hashtext('autorizacao.gestores_de_acesso'));",
            cancellationToken);

    public async Task<int> ContarGestoresAtivosAsync(
        Guid perfilId, Guid exceto, DateTimeOffset agora, CancellationToken cancellationToken = default) =>
        await _context.Concessoes
            .AsNoTracking()
            .Where(c => c.PerfilId == perfilId
                && c.Id != exceto
                && c.EscopoTipo == TipoDeEscopo.Instituicao
                && c.RevogadaEm == null
                && (c.ValidaAte == null || c.ValidaAte > agora))
            .Select(c => new { c.BeneficiarioEmissor, c.BeneficiarioSub })
            .Distinct()
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<TitularComEmail>> ListarTitularesAtivosComEmailAsync(
        Guid perfilId, DateTimeOffset agora, CancellationToken cancellationToken = default)
    {
        var titulares = await _context.Concessoes
            .AsNoTracking()
            .Where(c => c.PerfilId == perfilId
                && c.EscopoTipo == TipoDeEscopo.Instituicao
                && c.RevogadaEm == null
                && (c.ValidaAte == null || c.ValidaAte > agora)
                && c.BeneficiarioEmail != null)
            .Select(c => new { c.BeneficiarioEmissor, c.BeneficiarioSub, c.BeneficiarioEmail })
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. titulares.Select(t => new TitularComEmail(new Identidade(t.BeneficiarioEmissor, t.BeneficiarioSub), t.BeneficiarioEmail!))];
    }

    // Uma só consulta traz a concessão e o nome do perfil: o perfil existe sempre, pela chave
    // estrangeira, e o join não duplica linha.
    private IQueryable<ConcessaoComPerfil> ComPerfil(IQueryable<Concessao> concessoes) =>
        from c in concessoes
        join p in _context.Perfis.AsNoTracking() on c.PerfilId equals p.Id
        select new ConcessaoComPerfil(c, p.Nome) { Id = c.Id };
}
