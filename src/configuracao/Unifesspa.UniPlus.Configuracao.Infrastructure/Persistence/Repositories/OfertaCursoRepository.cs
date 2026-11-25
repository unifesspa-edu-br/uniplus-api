namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories;

using System.Linq.Expressions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Configurations;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories.Ordenacao;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Pagination;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via DI em ConfiguracaoInfrastructureRegistration.")]
public sealed class OfertaCursoRepository : IOfertaCursoRepository
{
    /// <summary>
    /// Caractere de escape do <c>LIKE</c>, o mesmo que a normalização insere antes
    /// dos curingas. Precisa ser declarado: a sobrecarga de dois argumentos do
    /// <c>ILike</c> no Npgsql emite <c>ESCAPE ''</c>, que desliga o escape — as
    /// barras viram texto e <c>%</c> e <c>_</c> voltam a ser curingas.
    /// </summary>
    private const string EscapeDoLike = @"\";

    private readonly UniPlusDbContext _dbContext;

    public OfertaCursoRepository(UniPlusDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public Task<OfertaCurso?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.OfertasCurso
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    public Task<bool> CodigoExisteEntreVivosAsync(string codigo, Guid? excluirId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(codigo);

        return _dbContext.OfertasCurso
            .AsNoTracking()
            .Where(o => excluirId == null || o.Id != excluirId)
            .AnyAsync(o => o.Codigo == codigo, cancellationToken);
    }

    public Task<OfertaCurso?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.OfertasCurso
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    public async Task<(IReadOnlyList<OfertaCurso> Itens, (string SortKey, Guid Id)? Anterior, (string SortKey, Guid Id)? Proximo, int? Total)>
        ListarPaginadoAsync(
            IReadOnlyList<SortField> ordenacao,
            string? busca,
            string? afterSortKey,
            Guid? afterId,
            int limit,
            PaginationDirection direction,
            Guid? cursoId,
            bool includeTotal,
            CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ordenacao);

        string? termo = NormalizacaoTextual.PrepararTermoDeBusca(busca);
        IQueryable<OfertaCursoOrdenada> query = OfertasDoRecorte(cursoId, termo);
        int? total = await ContarSeSolicitadoAsync(query, includeTotal, cancellationToken).ConfigureAwait(false);

        OrderedKeysetPage<OfertaCursoOrdenada> page = await OrderedKeysetCursor
            .ApplyAsync(query, OrdenacaoDeCursos.DeOfertas(ordenacao, RecorteDeOferta(termo, cursoId)), afterSortKey, afterId, limit, direction, cancellationToken)
            .ConfigureAwait(false);

        return ([.. page.Items.Select(static linha => linha.Entidade)], page.Previous, page.Next, total);
    }

    /// <summary>
    /// O recorte opcional por curso (issue #755) é aplicado ANTES do keyset para que os EXISTS de prev/next o herdem; é
    /// apoiado por ix_ofertas_de_curso_curso_id. A busca alcança o curso ofertado pela coluna já normalizada, o código do
    /// curso, o código da oferta e a sigla da unidade — todos disponíveis sem junção adicional.
    /// </summary>
    private IQueryable<OfertaCursoOrdenada> OfertasDoRecorte(Guid? cursoId, string? termo)
    {
        IQueryable<OfertaCurso> ofertas = _dbContext.OfertasCurso.AsNoTracking();
        if (cursoId is { } curso)
        {
            ofertas = ofertas.Where(o => o.CursoId == curso);
        }

        IQueryable<OfertaCursoOrdenada> query = ComOrdenacaoDoCurso(ofertas);
        return termo is null ? query : ComBusca(query, termo);
    }

    /// <summary>
    /// A oferta é ordenada pelo curso que oferta, e não navega até ele — as colunas do curso vêm desta junção. O filtro de
    /// soft-delete de cada lado é aplicado por convenção, então curso removido não ressuscita oferta.
    /// </summary>
    private IQueryable<OfertaCursoOrdenada> ComOrdenacaoDoCurso(IQueryable<OfertaCurso> ofertas) =>
        ofertas.Join(_dbContext.Cursos.AsNoTracking(), static oferta => oferta.CursoId, static curso => curso.Id, OfertaComOrdenacaoDoCurso);

    private static readonly Expression<Func<OfertaCurso, Curso, OfertaCursoOrdenada>> OfertaComOrdenacaoDoCurso = (oferta, curso) =>
        new OfertaCursoOrdenada
        {
            Id = oferta.Id,
            NomeOrdenacao = EF.Property<string>(curso, CursoConfiguration.NomeOrdenacaoPropriedade),
            Codigo = curso.Codigo,
            OfertaCodigo = oferta.Codigo,
            UnidadeSigla = oferta.UnidadeOfertante.Sigla,
            ProgramaDeOferta = oferta.ProgramaDeOferta,
            FormatoPedagogico = oferta.FormatoPedagogico,
            RegimeDeFuncionamento = oferta.RegimeDeFuncionamento,
            RegimeDeTurno = oferta.RegimeDeTurno,
            CriadoEm = oferta.CreatedAt,
            Entidade = oferta,
        };

    private static IQueryable<OfertaCursoOrdenada> ComBusca(IQueryable<OfertaCursoOrdenada> query, string termo)
    {
        string padrao = "%" + termo + "%";
        return query.Where(o =>
            EF.Functions.ILike(o.NomeOrdenacao, padrao, EscapeDoLike)
            || EF.Functions.ILike(FuncoesDaPlataforma.NormalizarParaComparacao(o.Codigo), padrao, EscapeDoLike)
            || EF.Functions.ILike(FuncoesDaPlataforma.NormalizarParaComparacao(o.OfertaCodigo), padrao, EscapeDoLike)
            || EF.Functions.ILike(FuncoesDaPlataforma.NormalizarParaComparacao(o.UnidadeSigla), padrao, EscapeDoLike));
    }

    /// <summary>
    /// A contagem sai da MESMA consulta que produziu a página (ADR-0026): o recorte por curso, a busca e o filtro de exclusão
    /// lógica já estão aplicados, e o keyset ainda não — um total que prometesse lista diferente da que o filtro traz seria
    /// rótulo que mente. Sem include_total nenhuma contagem é disparada: quem não pede não paga.
    /// </summary>
    private static async Task<int?> ContarSeSolicitadoAsync(IQueryable<OfertaCursoOrdenada> query, bool incluirTotal, CancellationToken cancellationToken) =>
        incluirTotal ? await query.CountAsync(cancellationToken).ConfigureAwait(false) : null;

    /// <summary>
    /// O que reduziu a coleção antes da paginação — termo pesquisado e curso
    /// filtrado. Entra na assinatura do cursor para que a continuação de um recorte
    /// não retome dentro de outro: sem isso, o cursor emitido para um curso
    /// retomaria na listagem de outro, a partir de uma posição que lá não existe.
    /// </summary>
    private static IReadOnlyList<string> RecorteDeOferta(string? termo, Guid? cursoId) =>
        [termo ?? string.Empty, cursoId?.ToString() ?? string.Empty];

    public async Task AdicionarAsync(OfertaCurso ofertaCurso, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ofertaCurso);
        await _dbContext.OfertasCurso.AddAsync(ofertaCurso, cancellationToken).ConfigureAwait(false);
    }

    public void Remover(OfertaCurso ofertaCurso)
    {
        ArgumentNullException.ThrowIfNull(ofertaCurso);
        _dbContext.OfertasCurso.Remove(ofertaCurso);
    }
}
