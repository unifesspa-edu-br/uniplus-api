namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Configurations;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories.Ordenacao;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Pagination;

public sealed class CursoRepository : ICursoRepository
{
    /// <summary>
    /// Caractere de escape do <c>LIKE</c>, o mesmo que a normalização insere antes
    /// dos curingas. Precisa ser declarado: a sobrecarga de dois argumentos do
    /// <c>ILike</c> no Npgsql emite <c>ESCAPE ''</c>, que desliga o escape — as
    /// barras viram texto e <c>%</c> e <c>_</c> voltam a ser curingas.
    /// </summary>
    private const string EscapeDoLike = @"\";

    private readonly UniPlusDbContext _dbContext;

    public CursoRepository(UniPlusDbContext dbContext)
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
            IReadOnlyList<SortField> ordenacao,
            TermoDeBusca? busca,
            string? afterSortKey,
            Guid? afterId,
            int limit,
            PaginationDirection direction,
            CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ordenacao);

        string? termo = NormalizacaoTextual.PrepararTermoDeBusca(busca);
        IQueryable<CursoOrdenado> consulta = Filtrar(ProjetarCursos(), termo);
        OrderedKeysetPage<CursoOrdenado> pagina = await OrderedKeysetCursor.ApplyAsync(
            consulta, OrdenacaoDeCursos.DeCursos(ordenacao, CondicoesDoFiltroDeCursos(termo)),
            afterSortKey, afterId, limit, direction, cancellationToken).ConfigureAwait(false);
        return ([.. pagina.Items.Select(static linha => linha.Entidade)], pagina.Previous, pagina.Next);
    }

    private IQueryable<CursoOrdenado> ProjetarCursos() => _dbContext.Cursos.AsNoTracking().Select(c => new CursoOrdenado
    {
        Id = c.Id,
        NomeOrdenacao = EF.Property<string>(c, CursoConfiguration.NomeOrdenacaoPropriedade),
        Codigo = c.Codigo,
        Grau = c.Grau,
        NivelEnsino = c.NivelEnsino,
        CriadoEm = c.CreatedAt,
        Entidade = c,
    });

    private static IQueryable<CursoOrdenado> Filtrar(IQueryable<CursoOrdenado> consulta, string? termo)
    {
        if (termo is null)
        {
            return consulta;
        }
        string padrao = "%" + termo + "%";
        return consulta.Where(c => EF.Functions.ILike(c.NomeOrdenacao, padrao, EscapeDoLike)
            || EF.Functions.ILike(FuncoesDaPlataforma.NormalizarParaComparacao(c.Codigo), padrao, EscapeDoLike));
    }

    /// <summary>
    /// O que reduziu a coleção antes da paginação. Entra na assinatura do cursor
    /// para que a continuação de uma busca não retome dentro de outra: o ponto de continuação é
    /// uma posição num conjunto, e trocar o conjunto a torna sem sentido.
    /// </summary>
    private static IReadOnlyList<string> CondicoesDoFiltroDeCursos(string? termo) =>
        [termo ?? string.Empty];

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

        return _dbContext.Cursos
            .AsNoTracking()
            .Where(c => excluirId == null || c.Id != excluirId)
            .AnyAsync(c => c.Codigo == codigo, cancellationToken);
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
