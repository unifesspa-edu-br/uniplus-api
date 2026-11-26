namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Pagination;

public sealed class ModeloFormularioRepository : IModeloFormularioRepository
{
    private readonly UniPlusDbContext _dbContext;

    public ModeloFormularioRepository(UniPlusDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public Task<ModeloFormulario?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken) =>
        _dbContext.ModelosFormulario.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

    public Task<ModeloFormulario?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken) =>
        _dbContext.ModelosFormulario.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

    public async Task<(IReadOnlyList<ModeloFormulario> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        string? tipoProcessoCodigo,
        bool? serveAFaseDeInscricao,
        bool? ativo,
        CancellationToken cancellationToken)
    {
        IQueryable<ModeloFormulario> consulta = Filtrar(_dbContext.ModelosFormulario.AsNoTracking(), tipoProcessoCodigo, serveAFaseDeInscricao, ativo);

        CursorKeysetPage<ModeloFormulario> page = await CursorKeyset
            .ApplyAsync(consulta, afterId, limit, direction, cancellationToken)
            .ConfigureAwait(false);
        return (page.Items, page.PrevAfterId, page.NextAfterId);
    }

    public async Task<IReadOnlyList<ModeloFormulario>> ListarTodosAsync(CancellationToken cancellationToken) =>
        await _dbContext.ModelosFormulario.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task AdicionarAsync(ModeloFormulario modelo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(modelo);
        await _dbContext.ModelosFormulario.AddAsync(modelo, cancellationToken).ConfigureAwait(false);
    }

    public Task<bool> CodigoExisteAsync(string codigo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(codigo);
        return _dbContext.ModelosFormulario.AsNoTracking().AnyAsync(m => m.Codigo == codigo, cancellationToken);
    }

    private static IQueryable<ModeloFormulario> Filtrar(
        IQueryable<ModeloFormulario> consulta, string? tipoProcessoCodigo, bool? serveAFaseDeInscricao, bool? ativo)
    {
        IQueryable<ModeloFormulario> filtrada = tipoProcessoCodigo is null
            ? consulta
            : consulta.Where(m => m.TipoProcessoCodigo == null || m.TipoProcessoCodigo == tipoProcessoCodigo);
        filtrada = serveAFaseDeInscricao is { } serve ? filtrada.Where(m => m.ServeAFaseDeInscricao == serve) : filtrada;
        return ativo is { } ativoFiltro ? filtrada.Where(m => m.Ativo == ativoFiltro) : filtrada;
    }
}
