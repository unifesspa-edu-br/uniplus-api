namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Kernel.Results;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via DI em ConfiguracaoInfrastructureRegistration.")]
public sealed class PesoAreaEnemRepository : IPesoAreaEnemRepository
{
    private readonly ConfiguracaoDbContext _dbContext;

    public PesoAreaEnemRepository(ConfiguracaoDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public Task<PesoAreaEnem?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.PesosAreaEnem
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public Task<PesoAreaEnem?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.PesosAreaEnem
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<(IReadOnlyList<PesoAreaEnem> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken)
    {
        // Keyset bidirecional (ADR-0089): ordenação por Id (Guid v7, ADR-0026/0032).
        CursorKeysetPage<PesoAreaEnem> page = await CursorKeyset
            .ApplyAsync(_dbContext.PesosAreaEnem.AsNoTracking(), afterId, limit, direction, cancellationToken)
            .ConfigureAwait(false);

        return (page.Items, page.PrevAfterId, page.NextAfterId);
    }

    public async Task AdicionarAsync(PesoAreaEnem peso, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(peso);
        await _dbContext.PesosAreaEnem.AddAsync(peso, cancellationToken).ConfigureAwait(false);
    }

    public void Remover(PesoAreaEnem peso)
    {
        ArgumentNullException.ThrowIfNull(peso);
        _dbContext.PesosAreaEnem.Remove(peso);
    }

    public Task<bool> ParExisteEntreVivosAsync(
        string resolucao,
        string grupoCurso,
        Guid? excluirId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resolucao);
        ArgumentNullException.ThrowIfNull(grupoCurso);

        // Um grupo fora do domínio nunca tem linha viva — evita query desnecessária
        // e garante que a comparação use o valor canônico normalizado (Trim).
        Result<GrupoCurso> grupoResult = GrupoCurso.Criar(grupoCurso);
        if (grupoResult.IsFailure)
        {
            return Task.FromResult(false);
        }

        // Espelha a normalização do agregado (Trim) para casar com o valor persistido.
        string resolucaoNorm = resolucao.Trim();
        GrupoCurso grupo = grupoResult.Value!;

        return _dbContext.PesosAreaEnem
            .AsNoTracking()
            .Where(p => excluirId == null || p.Id != excluirId)
            .AnyAsync(p => p.Resolucao == resolucaoNorm && p.GrupoCurso == grupo, cancellationToken);
    }
}
