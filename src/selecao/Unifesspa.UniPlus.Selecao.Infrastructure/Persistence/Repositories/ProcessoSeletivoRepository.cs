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
        // Lock pessimista da linha raiz do agregado (revisão do PR #791,
        // Story #759 T4): serializa handlers concorrentes (todo Definir* e
        // Publicar) que carregam o MESMO processo — sem isso, um Definir*
        // que leu Status=Rascunho antes de uma publicação concorrente pode
        // persistir mutação DEPOIS do SnapshotPublicacao já ter sido
        // congelado, furando RN08/CA-04 sem que o guard em memória
        // (MutacaoBloqueadaPosPublicacao) tenha visibilidade da publicação
        // alheia. SELECT ... FOR UPDATE roda na MESMA transação ambiente do
        // Wolverine (EnrollDbContextInTransaction) — a segunda transação
        // concorrente bloqueia aqui até a primeira committar ou reverter.
        await _context.Database
            .ExecuteSqlInterpolatedAsync($"SELECT 1 FROM selecao.processos_seletivos WHERE id = {id} FOR UPDATE", cancellationToken)
            .ConfigureAwait(false);

        return await _context.ProcessosSeletivos
            .Include(p => p.Etapas)
            .Include(p => p.OfertaAtendimento!).ThenInclude(o => o.Condicoes)
            .Include(p => p.OfertaAtendimento!).ThenInclude(o => o.Recursos)
            .Include(p => p.OfertaAtendimento!).ThenInclude(o => o.TiposDeficiencia)
            .Include(p => p.DistribuicaoVagas).ThenInclude(d => d.Modalidades)
            .Include(p => p.BonusRegional)
            .Include(p => p.CriteriosDesempate)
            .Include(p => p.Classificacao!).ThenInclude(c => c.RegrasEliminacao)
            // Editais carregados para a retificação (T5 #786) validar que o
            // edital retificado é o vigente deste processo (ProcessoSeletivo.Retificar).
            .Include(p => p.Editais)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AdicionarSnapshotPublicacaoAsync(SnapshotPublicacao snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        await _context.SnapshotsPublicacao.AddAsync(snapshot, cancellationToken).ConfigureAwait(false);
    }

    public async Task<(Edital Edital, SnapshotPublicacao Snapshot)?> ObterSnapshotVigenteAsync(
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

        // Publicação vigente = Edital publicado de MAIOR data ≤ instante
        // (ADR-0075/0076). ux_editais_processo_data_publicacao garante que não
        // há empate, então OrderByDescending().FirstOrDefault() é determinístico.
        // O EXISTS através de ProcessosSeletivos herda o filtro global de
        // soft-delete (ProcessoSeletivo é SoftDeletableEntity; Edital não é):
        // um processo excluído logicamente não vaza seu snapshot congelado —
        // cai no mesmo caminho 404 que o resto da API, coerente com ExisteAsync.
        Edital? edital = await _context.Editais
            .AsNoTracking()
            .Where(e => e.ProcessoSeletivoId == processoSeletivoId
                && e.DataPublicacao != null
                && e.DataPublicacao <= instanteUtc
                && _context.ProcessosSeletivos.Any(p => p.Id == processoSeletivoId))
            .OrderByDescending(e => e.DataPublicacao)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (edital is null)
        {
            return null;
        }

        // Todo Edital publicado tem exatamente um snapshot congelado 1:1 (T4,
        // ux_snapshot_publicacao_edital_id) — a ausência aqui seria estado
        // inconsistente e é tratada como "sem vigente" pelo handler.
        SnapshotPublicacao? snapshot = await _context.SnapshotsPublicacao
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.EditalId == edital.Id, cancellationToken)
            .ConfigureAwait(false);

        return snapshot is null ? null : (edital, snapshot);
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
