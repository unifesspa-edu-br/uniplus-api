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
public sealed class OfertaCursoRepository : IOfertaCursoRepository
{
    /// <summary>
    /// Caractere de escape do <c>LIKE</c>, o mesmo que a normalização insere antes
    /// dos curingas. Precisa ser declarado: a sobrecarga de dois argumentos do
    /// <c>ILike</c> no Npgsql emite <c>ESCAPE ''</c>, que desliga o escape — as
    /// barras viram texto e <c>%</c> e <c>_</c> voltam a ser curingas.
    /// </summary>
    private const string EscapeDoLike = @"\";

    private readonly ConfiguracaoDbContext _dbContext;

    public OfertaCursoRepository(ConfiguracaoDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public Task<OfertaCurso?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.OfertasCurso
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    public Task<OfertaCurso?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.OfertasCurso
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    public async Task<(IReadOnlyList<OfertaCurso> Itens, (string SortKey, Guid Id)? Anterior, (string SortKey, Guid Id)? Proximo)>
        ListarPaginadoAsync(
            IReadOnlyList<SortField> ordenacao,
            string? busca,
            string? afterSortKey,
            Guid? afterId,
            int limit,
            PaginationDirection direction,
            Guid? cursoId,
            CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ordenacao);

        IQueryable<OfertaCurso> ofertas = _dbContext.OfertasCurso.AsNoTracking();

        // Filtro opcional por curso (issue #755): aplicado ANTES do keyset para que
        // os EXISTS de prev/next herdem o recorte. Index-backed por ix_oferta_curso_curso_id.
        if (cursoId is { } curso)
        {
            ofertas = ofertas.Where(o => o.CursoId == curso);
        }

        // A oferta é ordenada pelo curso que oferta, e não navega até ele — as
        // colunas do curso vêm desta junção. O filtro de soft-delete de cada lado é
        // aplicado por convenção, então curso removido não ressuscita oferta.
        IQueryable<OfertaCursoOrdenada> query =
            from oferta in ofertas
            join c in _dbContext.Cursos.AsNoTracking() on oferta.CursoId equals c.Id
            select new OfertaCursoOrdenada
            {
                Id = oferta.Id,
                NomeOrdenacao = EF.Property<string>(c, CursoConfiguration.NomeOrdenacaoPropriedade),
                Codigo = c.Codigo,
                UnidadeSigla = oferta.UnidadeOfertante.Sigla,
                ProgramaDeOferta = oferta.ProgramaDeOferta,
                FormatoPedagogico = oferta.FormatoPedagogico,
                RegimeDeFuncionamento = oferta.RegimeDeFuncionamento,
                RegimeDeTurno = oferta.RegimeDeTurno,
                CriadoEm = oferta.CreatedAt,
                Entidade = oferta,
            };

        // A busca alcança o curso ofertado pela coluna já normalizada, o código do
        // curso e a sigla da unidade — as três disponíveis sem junção adicional.
        string? termo = NormalizacaoTextual.PrepararTermoDeBusca(busca);
        if (termo is not null)
        {
            string padrao = "%" + termo + "%";
            query = query.Where(o =>
                EF.Functions.ILike(o.NomeOrdenacao, padrao, EscapeDoLike)
                || EF.Functions.ILike(PgFunctions.NormalizarParaComparacao(o.Codigo), padrao, EscapeDoLike)
                || EF.Functions.ILike(PgFunctions.NormalizarParaComparacao(o.UnidadeSigla), padrao, EscapeDoLike));
        }

        OrderedKeysetPage<OfertaCursoOrdenada> page = await OrderedKeysetCursor
            .ApplyAsync(
                query,
                OrdenacaoDeCursos.DeOfertas(ordenacao, RecorteDeOferta(termo, cursoId)),
                afterSortKey,
                afterId,
                limit,
                direction,
                cancellationToken)
            .ConfigureAwait(false);

        return ([.. page.Items.Select(static linha => linha.Entidade)], page.Previous, page.Next);
    }

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
