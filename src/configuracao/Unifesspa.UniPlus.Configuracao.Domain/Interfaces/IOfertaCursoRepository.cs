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
    /// (ADR-0026 + ADR-0089), na ordenação pedida (ADR-0094), restritas ao termo
    /// pesquisado e ao curso informado. Devolve as âncoras de <c>prev</c>/<c>next</c>
    /// — o par chave de ordenação + <c>Id</c>, nulo quando não há aquele lado.
    /// </summary>
    /// <param name="ordenacao">
    /// Campos de ordenação já validados contra o catálogo do recurso, na ordem de
    /// prioridade. Nunca vazio: sem escolha do cliente, vale a ordem padrão.
    /// </param>
    /// <param name="busca">
    /// Texto pesquisado sobre nome e código do curso ofertado e sigla da unidade
    /// ofertante, insensível a caixa e acento; <c>null</c> ou em branco lista tudo.
    /// </param>
    /// <param name="afterSortKey">
    /// Chave de ordenação da âncora de continuação; <c>null</c> na primeira página.
    /// </param>
    /// <param name="cursoId">
    /// Filtro opcional (issue #755): restringe às ofertas do curso informado antes
    /// do keyset — itens e âncoras respeitam o recorte; <c>null</c> lista todas.
    /// </param>
    Task<(IReadOnlyList<OfertaCurso> Itens, (string SortKey, Guid Id)? Anterior, (string SortKey, Guid Id)? Proximo)>
        ListarPaginadoAsync(
            IReadOnlyList<SortField> ordenacao,
            string? busca,
            string? afterSortKey,
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
