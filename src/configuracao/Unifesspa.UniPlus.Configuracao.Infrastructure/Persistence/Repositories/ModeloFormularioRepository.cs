namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Regras.Formularios;

public sealed class ModeloFormularioRepository : IModeloFormularioRepository
{
    private readonly ConfiguracaoDbContext _dbContext;

    public ModeloFormularioRepository(ConfiguracaoDbContext dbContext)
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
        FinalidadeFormulario? finalidade,
        bool? ativo,
        CancellationToken cancellationToken)
    {
        IQueryable<ModeloFormulario> consulta = _dbContext.ModelosFormulario.AsNoTracking();
        if (tipoProcessoCodigo is not null)
        {
            consulta = consulta.Where(m => m.TipoProcessoCodigo == null || m.TipoProcessoCodigo == tipoProcessoCodigo);
        }

        if (finalidade is { } finalidadeFiltro)
        {
            consulta = consulta.Where(m => m.Finalidade == finalidadeFiltro);
        }

        if (ativo is { } ativoFiltro)
        {
            consulta = consulta.Where(m => m.Ativo == ativoFiltro);
        }

        CursorKeysetPage<ModeloFormulario> page = await CursorKeyset
            .ApplyAsync(consulta, afterId, limit, direction, cancellationToken)
            .ConfigureAwait(false);
        return (page.Items, page.PrevAfterId, page.NextAfterId);
    }

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
}
