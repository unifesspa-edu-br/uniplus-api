namespace Unifesspa.UniPlus.Configuracao.Domain.Interfaces;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Repositório da entidade <see cref="OfertaCurso"/> (ADR-0054: banco isolado
/// <c>uniplus_configuracao</c>). Todas as leituras excluem registros
/// soft-deleted via query filter por convenção. Não há chave natural única —
/// o repositório não expõe checagem de unicidade.
/// </summary>
public interface IOfertaCursoRepository
{
    /// <summary>Carrega a oferta rastreada pelo contexto, para mutação.</summary>
    Task<OfertaCurso?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Carrega a oferta para leitura (<c>AsNoTracking</c>) — projeção em DTO.</summary>
    Task<OfertaCurso?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Lista ofertas de curso vivas paginadas por cursor keyset bidirecional
    /// (ADR-0026 + ADR-0089): ordena por <c>Id</c> (Guid v7, ADR-0032) e devolve
    /// as âncoras de <c>prev</c>/<c>next</c> (nulas quando não há aquele lado).
    /// O filtro opcional <paramref name="cursoId"/> (issue #755) restringe às
    /// ofertas do curso informado antes do keyset — itens e âncoras respeitam o
    /// recorte; <c>null</c> lista todas.
    /// </summary>
    Task<(IReadOnlyList<OfertaCurso> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        Guid? cursoId,
        CancellationToken cancellationToken);

    Task AdicionarAsync(OfertaCurso ofertaCurso, CancellationToken cancellationToken);

    /// <summary>
    /// Marca a oferta para remoção; o <c>SoftDeleteInterceptor</c> converte em
    /// soft-delete preenchendo <c>DeletedBy</c>/<c>DeletedAt</c>.
    /// </summary>
    void Remover(OfertaCurso ofertaCurso);
}
