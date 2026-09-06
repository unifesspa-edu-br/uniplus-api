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
            string? afterSortKey,
            Guid? afterId,
            int limit,
            PaginationDirection direction,
            Guid? cursoId,
            CancellationToken cancellationToken)
    {
        IQueryable<OfertaCurso> ofertas = _dbContext.OfertasCurso.AsNoTracking();

        // Filtro opcional por curso (issue #755): aplicado ANTES do keyset para que
        // os EXISTS de prev/next herdem o recorte. Index-backed por ix_oferta_curso_curso_id.
        if (cursoId is { } curso)
        {
            ofertas = ofertas.Where(o => o.CursoId == curso);
        }

        // A oferta é ordenada pelo curso que oferta, e não navega até ele — a chave
        // de ordenação vem desta junção. O filtro de soft-delete de cada lado é
        // aplicado por convenção, então curso removido não ressuscita oferta.
        IQueryable<OfertaCursoOrdenada> query =
            from oferta in ofertas
            join c in _dbContext.Cursos.AsNoTracking() on oferta.CursoId equals c.Id
            select new OfertaCursoOrdenada
            {
                Id = oferta.Id,
                NomeOrdenacao = EF.Property<string>(c, CursoConfiguration.NomeOrdenacaoPropriedade),
                Codigo = c.Codigo,
                Entidade = oferta,
            };

        KeysetOrdenadoPage<OfertaCursoOrdenada> page = await KeysetOrdenadoCursor
            .ApplyAsync(
                query,
                OrdenacaoAlfabeticaDoCurso.OfertasCurso,
                afterSortKey,
                afterId,
                limit,
                direction,
                cancellationToken)
            .ConfigureAwait(false);

        return ([.. page.Items.Select(static linha => linha.Entidade)], page.Anterior, page.Proximo);
    }

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
