namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Kernel.Results;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via DI em ConfiguracaoInfrastructureRegistration.")]
public sealed class TipoDeficienciaRepository : ITipoDeficienciaRepository
{
    private readonly UniPlusDbContext _dbContext;

    public TipoDeficienciaRepository(UniPlusDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public Task<TipoDeficiencia?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.TiposDeficiencia
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<TipoDeficiencia>> ListarVivosParaLeituraAsync(CancellationToken cancellationToken) =>
        await _dbContext.TiposDeficiencia.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task<TipoDeficiencia?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.TiposDeficiencia
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<(IReadOnlyList<TipoDeficiencia> Itens, (string SortKey, Guid Id)? Anterior, (string SortKey, Guid Id)? Proximo)> ListarPaginadoAsync(
        string? afterSortKey,
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken)
    {
        // Keyset bidirecional (ADR-0089): ordenação por Id (Guid v7, ADR-0026/0032).
        OrderedKeysetPage<TipoDeficiencia> page = await CursorKeyset
            .ApplyAsync(_dbContext.TiposDeficiencia.AsNoTracking(), CondicoesDoFiltro.Nenhuma, afterSortKey, afterId, limit, direction, cancellationToken)
            .ConfigureAwait(false);

        return (page.Items, page.Previous, page.Next);
    }

    public async Task AdicionarAsync(TipoDeficiencia tipo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tipo);
        await _dbContext.TiposDeficiencia.AddAsync(tipo, cancellationToken).ConfigureAwait(false);
    }

    public void Remover(TipoDeficiencia tipo)
    {
        ArgumentNullException.ThrowIfNull(tipo);
        _dbContext.TiposDeficiencia.Remove(tipo);
    }

    public Task<bool> CodigoExisteEntreVivosAsync(
        string codigo,
        Guid? excluirId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(codigo);

        // Um código fora do formato nunca tem tipo vivo — evita query desnecessária e
        // garante que a comparação use o valor canônico normalizado (Trim).
        // Case-sensitive (default do Postgres) — alinhado ao índice único.
        Result<CodigoTipoDeficiencia> codigoResult = CodigoTipoDeficiencia.Criar(codigo);
        if (codigoResult.IsFailure)
        {
            return Task.FromResult(false);
        }

        CodigoTipoDeficiencia codigoVo = codigoResult.Value!;

        return _dbContext.TiposDeficiencia
            .AsNoTracking()
            .Where(t => excluirId == null || t.Id != excluirId)
            .AnyAsync(t => t.Codigo == codigoVo, cancellationToken);
    }

    public Task<bool> NomeExisteEntreVivosAsync(
        string nome,
        Guid? excluirId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(nome);

        // Espelha a normalização do agregado (Trim) para casar com o valor persistido.
        // Comparação case-sensitive (default do Postgres) — alinhada ao índice único.
        string nomeNorm = nome.Trim();

        return _dbContext.TiposDeficiencia
            .AsNoTracking()
            .Where(t => excluirId == null || t.Id != excluirId)
            .AnyAsync(t => t.Nome == nomeNorm, cancellationToken);
    }
}
