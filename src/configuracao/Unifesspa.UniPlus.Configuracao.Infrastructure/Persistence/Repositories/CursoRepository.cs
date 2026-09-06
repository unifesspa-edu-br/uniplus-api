namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Configurations;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories.Ordenacao;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Pagination;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via DI em ConfiguracaoInfrastructureRegistration.")]
public sealed class CursoRepository : ICursoRepository
{
    private readonly ConfiguracaoDbContext _dbContext;

    public CursoRepository(ConfiguracaoDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public Task<Curso?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.Cursos
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public Task<Curso?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.Cursos
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<(IReadOnlyList<Curso> Itens, (string SortKey, Guid Id)? Anterior, (string SortKey, Guid Id)? Proximo)>
        ListarPaginadoAsync(
            string? afterSortKey,
            Guid? afterId,
            int limit,
            PaginationDirection direction,
            CancellationToken cancellationToken)
    {
        // A chave de ordenação alfabética é coluna gerada mapeada como propriedade
        // sombra — a entidade materializada não a carrega, então a listagem projeta
        // uma linha que a traz junto e serve de âncora ao motor de paginação.
        IQueryable<CursoOrdenado> query = _dbContext.Cursos
            .AsNoTracking()
            .Select(c => new CursoOrdenado
            {
                Id = c.Id,
                NomeOrdenacao = EF.Property<string>(c, CursoConfiguration.NomeOrdenacaoPropriedade),
                Codigo = c.Codigo,
                Entidade = c,
            });

        KeysetOrdenadoPage<CursoOrdenado> page = await KeysetOrdenadoCursor
            .ApplyAsync(
                query,
                OrdenacaoAlfabeticaDoCurso.Cursos,
                afterSortKey,
                afterId,
                limit,
                direction,
                cancellationToken)
            .ConfigureAwait(false);

        return ([.. page.Items.Select(static linha => linha.Entidade)], page.Anterior, page.Proximo);
    }

    public async Task AdicionarAsync(Curso curso, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(curso);
        await _dbContext.Cursos.AddAsync(curso, cancellationToken).ConfigureAwait(false);
    }

    public void Remover(Curso curso)
    {
        ArgumentNullException.ThrowIfNull(curso);
        _dbContext.Cursos.Remove(curso);
    }

    public Task<bool> CodigoExisteEntreVivosAsync(
        string codigo,
        Guid? excluirId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(codigo);

        // Espelha a normalização do agregado (Trim) para casar com o valor persistido.
        // Comparação case-sensitive (default do Postgres) — alinhada ao índice único.
        string codigoNorm = codigo.Trim();

        return _dbContext.Cursos
            .AsNoTracking()
            .Where(c => excluirId == null || c.Id != excluirId)
            .AnyAsync(c => c.Codigo == codigoNorm, cancellationToken);
    }

    public Task<bool> ReferenciadoPorOfertaCursoVivaAsync(Guid cursoId, CancellationToken cancellationToken)
    {
        // EXISTS sobre ofertas vivas (#749): o query filter global de soft-delete
        // já restringe a ofertas não removidas — o soft-delete da oferta libera o
        // curso para remoção.
        return _dbContext.OfertasCurso
            .AsNoTracking()
            .AnyAsync(o => o.CursoId == cursoId, cancellationToken);
    }
}
