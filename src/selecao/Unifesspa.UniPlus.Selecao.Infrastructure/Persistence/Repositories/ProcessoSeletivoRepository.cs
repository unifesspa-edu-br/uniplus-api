namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Domain.Entities;
using Domain.Interfaces;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Pagination;

public sealed class ProcessoSeletivoRepository : IProcessoSeletivoRepository
{
    private readonly SelecaoDbContext _context;
    private readonly TimeProvider _timeProvider;

    public ProcessoSeletivoRepository(SelecaoDbContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task<ProcessoSeletivo?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.ProcessosSeletivos
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ProcessoSeletivo>> ObterTodosAsync(CancellationToken cancellationToken = default)
    {
        return await _context.ProcessosSeletivos
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AdicionarAsync(ProcessoSeletivo entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        await _context.ProcessosSeletivos.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public void Atualizar(ProcessoSeletivo entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        _context.ProcessosSeletivos.Update(entity);
    }

    public void Remover(ProcessoSeletivo entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        entity.MarkAsDeleted("system", _timeProvider.GetUtcNow());
    }

    public async Task<ProcessoSeletivo?> ObterComConfiguracaoAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Carregamento de LEITURA: sem lock e sem a sessão editorial. O FOR UPDATE que
        // vivia aqui existia para serializar os handlers de mutação — e eles agora têm
        // carregamento próprio (ObterParaMutacaoAsync, ADR-0110 D4). Mantê-lo aqui faria
        // duas consultas GET concorrentes ao mesmo processo se serializarem uma na outra,
        // sem que nenhuma delas escreva coisa alguma.
        return await ComConfiguracao(_context.ProcessosSeletivos)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ProcessoSeletivo?> ObterParaMutacaoAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Lock pessimista da linha raiz do agregado (revisão do PR #791, Story #759 T4):
        // serializa handlers concorrentes (os seis Definir*, a abertura/fechamento da
        // sessão editorial, Publicar e Retificar) que carregam o MESMO processo — sem
        // isso, um Definir* que leu Status=Rascunho antes de uma publicação concorrente
        // pode persistir mutação DEPOIS de a versão já ter sido congelada, furando a RN08
        // sem que o guard em memória tenha visibilidade da publicação alheia. O
        // SELECT ... FOR UPDATE roda na MESMA transação ambiente do Wolverine
        // (EnrollDbContextInTransaction) — a segunda transação concorrente bloqueia aqui
        // até a primeira committar ou reverter.
        await _context.Database
            .ExecuteSqlInterpolatedAsync($"SELECT 1 FROM selecao.processos_seletivos WHERE id = {id} FOR UPDATE", cancellationToken)
            .ConfigureAwait(false);

        // O Rascunho é o que distingue este carregamento do de leitura: é dele que a
        // allowlist da D4 depende, e um null "por não ter sido carregado" recusaria uma
        // edição legítima.
        return await ComConfiguracao(_context.ProcessosSeletivos)
            .Include(p => p.Rascunho)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    private static IQueryable<ProcessoSeletivo> ComConfiguracao(IQueryable<ProcessoSeletivo> query) =>
        query
            .Include(p => p.Etapas)
            .Include(p => p.OfertaAtendimento!).ThenInclude(o => o.Condicoes)
            .Include(p => p.OfertaAtendimento!).ThenInclude(o => o.Recursos)
            .Include(p => p.OfertaAtendimento!).ThenInclude(o => o.TiposDeficiencia)
            .Include(p => p.DistribuicaoVagas).ThenInclude(d => d.Modalidades)
            .Include(p => p.BonusRegional)
            .Include(p => p.CriteriosDesempate)
            .Include(p => p.Classificacao!).ThenInclude(c => c.RegrasEliminacao);

    public async Task AdicionarVersaoConfiguracaoAsync(VersaoConfiguracao versao, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(versao);
        await _context.VersoesConfiguracao.AddAsync(versao, cancellationToken).ConfigureAwait(false);
    }

    public async Task<VersaoConfiguracao?> ObterVersaoAtualAsync(
        Guid processoSeletivoId,
        CancellationToken cancellationToken = default)
    {
        // A versão corrente é a de maior NÚMERO — não a de maior vigência nem a
        // mais recente por id. A numeração é contígua e monotônica por processo
        // (ux_versoes_configuracao_processo_numero + trigger de sucessão), então
        // o topo da cadeia é inequívoco mesmo se duas versões compartilharem o
        // mesmo instante de vigência (permitido por desenho — ADR-0104).
        return await _context.VersoesConfiguracao
            .AsNoTracking()
            .Where(v => v.ProcessoSeletivoId == processoSeletivoId)
            .OrderByDescending(v => v.NumeroVersao)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Guid>> ObterAtosCriadoresAsync(
        Guid processoSeletivoId,
        CancellationToken cancellationToken = default)
    {
        return await _context.VersoesConfiguracao
            .AsNoTracking()
            .Where(v => v.ProcessoSeletivoId == processoSeletivoId)
            .OrderBy(v => v.NumeroVersao)
            .Select(v => v.AtoCriadorId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<VersaoConfiguracao?> ObterVersaoVigenteAsync(
        Guid processoSeletivoId,
        DateTimeOffset instante,
        CancellationToken cancellationToken = default)
    {
        // Npgsql exige DateTimeOffset em UTC (offset zero) ao comparar contra
        // colunas timestamptz — um instante com offset não-UTC (ex.: -03:00
        // vindo de um Accept RFC 3339) falharia na execução da query. Normaliza
        // aqui, no boundary que dona a interação com o Npgsql, preservando o
        // mesmo instante; qualquer chamador fica DB-safe.
        DateTimeOffset instanteUtc = instante.ToUniversalTime();

        // Configuração vigente = VERSÃO de maior vigente_a_partir_de ≤ instante,
        // desempatada pelo número (ADR-0075/0076/0104). Nenhum atributo do ato entra
        // na query: o que ordena é o relógio do sistema, e não a data que o documento
        // declara — que a retificação republica inalterada, e que um acervo migrado
        // pode trazer regredida. O seletor é, por isso, imune a tipos de ato;
        // ix_versoes_configuracao_processo_vigencia o cobre.
        //
        // O empate de instante é permitido por desenho (não há unicidade sobre
        // vigente_a_partir_de): quando o relógio regride, VersaoConfiguracao.Suceder
        // ancora a sucessora no instante da anterior, e é o número decrescente que
        // elege a mais nova.
        //
        // O EXISTS através de ProcessosSeletivos herda o filtro global de
        // soft-delete (ProcessoSeletivo é SoftDeletableEntity; VersaoConfiguracao
        // é forense, sem exclusão lógica própria): um processo excluído
        // logicamente não vaza sua configuração congelada — cai no mesmo caminho
        // 404 que o resto da API, coerente com ExisteAsync.
        return await _context.VersoesConfiguracao
            .AsNoTracking()
            .Where(v => v.ProcessoSeletivoId == processoSeletivoId
                && v.VigenteAPartirDe <= instanteUtc
                && _context.ProcessosSeletivos.Any(p => p.Id == processoSeletivoId))
            .OrderByDescending(v => v.VigenteAPartirDe)
            .ThenByDescending(v => v.NumeroVersao)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> ExisteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.ProcessosSeletivos
            .AsNoTracking()
            .AnyAsync(p => p.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<(IReadOnlyList<ProcessoSeletivo> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken = default)
    {
        // Keyset bidirecional (ADR-0089): ordenação, âncora, probe n+1, reversão
        // e flags ficam no helper. Com Guid v7 (ADR-0032) a ordem por Id é cronológica.
        IQueryable<ProcessoSeletivo> query = _context.ProcessosSeletivos.AsNoTracking();

        CursorKeysetPage<ProcessoSeletivo> page = await CursorKeyset
            .ApplyAsync(query, afterId, limit, direction, cancellationToken)
            .ConfigureAwait(false);

        return (page.Items, page.PrevAfterId, page.NextAfterId);
    }
}
