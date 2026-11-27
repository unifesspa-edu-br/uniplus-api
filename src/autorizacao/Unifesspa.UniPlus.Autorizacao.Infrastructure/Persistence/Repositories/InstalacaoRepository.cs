namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>Repositório das instalações dos primeiros gestores e das tentativas de confirmá-las.</summary>
public sealed class InstalacaoRepository : IInstalacaoRepository
{
    private const string TravaDaLinha =
        $"SELECT 1 FROM {AutorizacaoContribuicao.NomeDoEsquema}.instalacoes WHERE id = {{0}} FOR UPDATE";

    private readonly UniPlusDbContext _context;

    public InstalacaoRepository(UniPlusDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public async Task<Instalacao?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _context.Instalacoes.FirstOrDefaultAsync(i => i.Id == id, cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<Instalacao>> ObterTodosAsync(CancellationToken cancellationToken = default) =>
        await _context.Instalacoes.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task AdicionarAsync(Instalacao entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        await _context.Instalacoes.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public void Atualizar(Instalacao entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        _context.Instalacoes.Update(entity);
    }

    public void Remover(Instalacao entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        // A instalação é registro de auditoria: encerra-se por estado, nunca se exclui.
        throw new NotSupportedException("Instalação não é removida — ela se encerra por cancelamento, expiração ou confirmação.");
    }

    public async Task<Instalacao?> ObterPendenteDoExecutorAsync(Identidade executor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(executor);

        return await _context.Instalacoes
            .Where(i => i.ExecutorEmissor == executor.Emissor && i.ExecutorContaId == executor.ContaId && Instalacao.SituacoesPendentes.Contains(i.Situacao))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Duas tentativas simultâneas do mesmo executor se enfileiram na linha da instalação: sem a trava, a que perde o
    /// conflito de concorrência descartaria o registro da própria tentativa. A trava dura até o fim da transação do
    /// handler, e a releitura depois dela enxerga o que a anterior confirmou.
    /// </summary>
    public async Task<Instalacao?> ObterAguardandoCodigoDoExecutorAsync(Identidade executor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(executor);

        if (await IdAguardandoCodigoDoExecutorAsync(executor, cancellationToken).ConfigureAwait(false) is not { } id)
        {
            return null;
        }

        await TravarParaAtualizarAsync(id, cancellationToken).ConfigureAwait(false);

        return await _context.Instalacoes
            .Where(i => i.Id == id && i.Situacao == SituacaoDaInstalacao.AguardandoCodigo)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Instalacao?> ObterDoExecutorAsync(Guid id, Identidade executor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(executor);

        return await _context.Instalacoes
            .Where(i => i.Id == id && i.ExecutorEmissor == executor.Emissor && i.ExecutorContaId == executor.ContaId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Instalacao?> ObterParaLeituraAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _context.Instalacoes.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id, cancellationToken).ConfigureAwait(false);

    public async Task AdicionarTentativaAsync(TentativaDeInstalacao tentativa, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tentativa);
        await _context.TentativasDeInstalacao.AddAsync(tentativa, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TentativaDeInstalacao>> ListarTentativasAsync(Guid instalacaoId, CancellationToken cancellationToken = default) =>
        await _context.TentativasDeInstalacao
            .AsNoTracking()
            .Where(t => t.InstalacaoId == instalacaoId)
            .OrderBy(t => t.Instante)
            .ThenBy(t => t.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<(IReadOnlyList<TentativaDeInstalacao> Itens, (string SortKey, Guid Id)? Anterior, (string SortKey, Guid Id)? Proximo)> ListarTentativasSemPendenciaAsync(
        string? afterSortKey,
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken = default)
    {
        OrderedKeysetPage<TentativaDeInstalacao> page = await CursorKeyset
            .ApplyAsync(
                _context.TentativasDeInstalacao.AsNoTracking().Where(t => t.InstalacaoId == null),
                CondicoesDoFiltro.Nenhuma,
                afterSortKey,
                afterId,
                limit,
                direction,
                cancellationToken)
            .ConfigureAwait(false);

        return (page.Items, page.Previous, page.Next);
    }

    private async Task<Guid?> IdAguardandoCodigoDoExecutorAsync(Identidade executor, CancellationToken cancellationToken) =>
        await _context.Instalacoes
            .AsNoTracking()
            .Where(i => i.ExecutorEmissor == executor.Emissor
                && i.ExecutorContaId == executor.ContaId
                && i.Situacao == SituacaoDaInstalacao.AguardandoCodigo)
            .Select(i => (Guid?)i.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

    private async Task TravarParaAtualizarAsync(Guid id, CancellationToken cancellationToken) =>
        await _context.Database.ExecuteSqlRawAsync(TravaDaLinha, [id], cancellationToken).ConfigureAwait(false);
}
