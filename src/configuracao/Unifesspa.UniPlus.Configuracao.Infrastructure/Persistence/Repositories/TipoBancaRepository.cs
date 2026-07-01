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
public sealed class TipoBancaRepository : ITipoBancaRepository
{
    private readonly ConfiguracaoDbContext _dbContext;

    public TipoBancaRepository(ConfiguracaoDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public Task<TipoBanca?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.TiposBanca
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    public Task<TipoBanca?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.TiposBanca
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    public async Task<(IReadOnlyList<TipoBanca> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken)
    {
        // Keyset bidirecional (ADR-0089): ordenação por Id (Guid v7, ADR-0026/0032).
        CursorKeysetPage<TipoBanca> page = await CursorKeyset
            .ApplyAsync(_dbContext.TiposBanca.AsNoTracking(), afterId, limit, direction, cancellationToken)
            .ConfigureAwait(false);

        return (page.Items, page.PrevAfterId, page.NextAfterId);
    }

    public async Task AdicionarAsync(TipoBanca banca, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(banca);
        await _dbContext.TiposBanca.AddAsync(banca, cancellationToken).ConfigureAwait(false);
    }

    public void Remover(TipoBanca banca)
    {
        ArgumentNullException.ThrowIfNull(banca);
        _dbContext.TiposBanca.Remove(banca);
    }

    public Task<bool> CodigoExisteEntreVivosAsync(
        string codigo,
        Guid? excluirId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(codigo);

        // Um código fora do formato nunca tem tipo de banca vivo — evita query
        // desnecessária e garante que a comparação use o valor canônico normalizado
        // (Trim). Case-sensitive (default do Postgres) — alinhado ao índice único.
        Result<CodigoBanca> codigoResult = CodigoBanca.Criar(codigo);
        if (codigoResult.IsFailure)
        {
            return Task.FromResult(false);
        }

        CodigoBanca codigoVo = codigoResult.Value!;

        return _dbContext.TiposBanca
            .AsNoTracking()
            .Where(b => excluirId == null || b.Id != excluirId)
            .AnyAsync(b => b.Codigo == codigoVo, cancellationToken);
    }
}
