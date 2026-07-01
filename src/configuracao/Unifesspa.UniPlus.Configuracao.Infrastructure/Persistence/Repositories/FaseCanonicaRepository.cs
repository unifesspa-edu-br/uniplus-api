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
public sealed class FaseCanonicaRepository : IFaseCanonicaRepository
{
    private readonly ConfiguracaoDbContext _dbContext;

    public FaseCanonicaRepository(ConfiguracaoDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public Task<FaseCanonica?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.FasesCanonicas
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
    }

    public Task<FaseCanonica?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.FasesCanonicas
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
    }

    public async Task<(IReadOnlyList<FaseCanonica> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken)
    {
        // Keyset bidirecional (ADR-0089): ordenação por Id (Guid v7, ADR-0026/0032).
        CursorKeysetPage<FaseCanonica> page = await CursorKeyset
            .ApplyAsync(_dbContext.FasesCanonicas.AsNoTracking(), afterId, limit, direction, cancellationToken)
            .ConfigureAwait(false);

        return (page.Items, page.PrevAfterId, page.NextAfterId);
    }

    public async Task AdicionarAsync(FaseCanonica fase, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fase);
        await _dbContext.FasesCanonicas.AddAsync(fase, cancellationToken).ConfigureAwait(false);
    }

    public void Remover(FaseCanonica fase)
    {
        ArgumentNullException.ThrowIfNull(fase);
        _dbContext.FasesCanonicas.Remove(fase);
    }

    public Task<bool> CodigoExisteEntreVivosAsync(
        string codigo,
        Guid? excluirId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(codigo);

        // Um código fora do formato nunca tem fase viva — evita query desnecessária
        // e garante que a comparação use o valor canônico normalizado (Trim).
        // Case-sensitive (default do Postgres) — alinhado ao índice único.
        Result<CodigoFase> codigoResult = CodigoFase.Criar(codigo);
        if (codigoResult.IsFailure)
        {
            return Task.FromResult(false);
        }

        CodigoFase codigoVo = codigoResult.Value!;

        return _dbContext.FasesCanonicas
            .AsNoTracking()
            .Where(f => excluirId == null || f.Id != excluirId)
            .AnyAsync(f => f.Codigo == codigoVo, cancellationToken);
    }
}
