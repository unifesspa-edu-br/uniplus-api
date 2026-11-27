namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
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

    public async Task<(IReadOnlyList<ModeloFormulario> Itens, (string SortKey, Guid Id)? Anterior, (string SortKey, Guid Id)? Proximo)> ListarPaginadoAsync(
        string? afterSortKey,
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        FiltroDosModelosDeFormulario filtro,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        OrderedKeysetPage<ModeloFormulario> page = await CursorKeyset
            .ApplyAsync(Filtrar(_dbContext.ModelosFormulario.AsNoTracking(), filtro), CondicoesDe(filtro), afterSortKey, afterId, limit, direction, cancellationToken)
            .ConfigureAwait(false);
        return (page.Items, page.Previous, page.Next);
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

    private static IQueryable<ModeloFormulario> Filtrar(IQueryable<ModeloFormulario> consulta, FiltroDosModelosDeFormulario filtro)
    {
        IQueryable<ModeloFormulario> filtrada = FiltrarPorTipoDeProcesso(consulta, filtro.TipoProcessoCodigo);
        filtrada = filtro.ServeAFaseDeInscricao is { } serve ? filtrada.Where(m => m.ServeAFaseDeInscricao == serve) : filtrada;
        return filtro.Ativo is { } ativo ? filtrada.Where(m => m.Ativo == ativo) : filtrada;
    }

    // O tipo de processo que o banco não grava não é de nenhum modelo: a consulta não traz nada e nem chega ao banco com o
    // texto, mas o resumo do cursor já foi conferido com ele.
    private static IQueryable<ModeloFormulario> FiltrarPorTipoDeProcesso(IQueryable<ModeloFormulario> consulta, string? tipoProcessoCodigo)
    {
        if (tipoProcessoCodigo is null)
        {
            return consulta;
        }

        return TextoGravavel.EhGravavel(tipoProcessoCodigo)
            ? consulta.Where(m => m.TipoProcessoCodigo == null || m.TipoProcessoCodigo == tipoProcessoCodigo)
            : consulta.Where(static _ => false);
    }

    private static CondicoesDoFiltro CondicoesDe(FiltroDosModelosDeFormulario filtro) =>
        CondicoesDoFiltro.De(
            ParteDoFiltro.De(filtro.TipoProcessoCodigo),
            ParteDoFiltro.De(filtro.ServeAFaseDeInscricao),
            ParteDoFiltro.De(filtro.Ativo));
}
