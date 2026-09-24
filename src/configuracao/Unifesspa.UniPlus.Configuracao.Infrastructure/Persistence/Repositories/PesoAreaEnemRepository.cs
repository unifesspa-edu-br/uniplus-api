namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

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

    public void RegistrarAtualizacao(PesoAreaEnem peso)
    {
        ArgumentNullException.ThrowIfNull(peso);

        // Entry() detecta as mudanças da própria linha, e Entries<>() as das áreas: não é
        // preciso varrer o ChangeTracker de novo à mão.
        EntityEntry<PesoAreaEnem> linha = _dbContext.Entry(peso);
        if (linha.State != EntityState.Unchanged)
        {
            // A própria linha mudou (ex.: base legal): o interceptor já carimba.
            return;
        }

        bool areasMudaram = _dbContext.ChangeTracker.Entries<PesoAreaEnemArea>()
            .Any(area => area.State is EntityState.Added or EntityState.Modified or EntityState.Deleted
                && area.Property<Guid>("PesoAreaEnemId").CurrentValue == peso.Id);
        if (!areasMudaram)
        {
            // Nada mudou: um PUT idêntico ao estado atual não é registrado como edição.
            return;
        }

        // Marca só o UpdatedAt: a linha vira Modified e o AuditableInterceptor carimba
        // UpdatedAt/UpdatedBy, sem regravar as demais colunas. Marcar a entrada inteira
        // regravaria is_deleted/deleted_* e desfaria uma remoção lógica concorrente.
        linha.Property(p => p.UpdatedAt).IsModified = true;
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

        // Um grupo fora do domínio nunca tem linha viva — evita query desnecessária.
        Result<GrupoCurso> grupoResult = GrupoCurso.Criar(grupoCurso);
        if (grupoResult.IsFailure)
        {
            return Task.FromResult(false);
        }

        // Espelha a normalização do agregado (aparar e NFC) para casar com o valor persistido;
        // texto que o agregado não gravaria não tem linha.
        string? resolucaoNorm = PesoAreaEnem.NormalizarResolucao(resolucao);
        if (resolucaoNorm is null)
        {
            return Task.FromResult(false);
        }

        GrupoCurso grupo = grupoResult.Value!;

        return _dbContext.PesosAreaEnem
            .AsNoTracking()
            .Where(p => excluirId == null || p.Id != excluirId)
            .AnyAsync(p => p.Resolucao == resolucaoNorm && p.GrupoCurso == grupo, cancellationToken);
    }
}
