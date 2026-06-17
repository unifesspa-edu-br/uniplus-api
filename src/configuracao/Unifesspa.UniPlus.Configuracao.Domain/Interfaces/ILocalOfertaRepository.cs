namespace Unifesspa.UniPlus.Configuracao.Domain.Interfaces;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Repositório da entidade <see cref="LocalOferta"/> (ADR-0054: banco isolado
/// <c>uniplus_configuracao</c>). Todas as leituras excluem registros
/// soft-deleted via query filter por convenção.
/// </summary>
public interface ILocalOfertaRepository
{
    /// <summary>Carrega o local rastreado pelo contexto, para mutação.</summary>
    Task<LocalOferta?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Carrega o local para leitura (<c>AsNoTracking</c>) — projeção em DTO.</summary>
    Task<LocalOferta?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Lista locais de oferta vivos paginados por cursor keyset bidirecional
    /// (ADR-0026 + ADR-0089): ordena por <c>Id</c> (Guid v7, ADR-0032) e devolve
    /// as âncoras de <c>prev</c>/<c>next</c> (nulas quando não há aquele lado).
    /// </summary>
    Task<(IReadOnlyList<LocalOferta> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken);

    Task AdicionarAsync(LocalOferta localOferta, CancellationToken cancellationToken);

    /// <summary>
    /// Marca o local para remoção; o <c>SoftDeleteInterceptor</c> converte em
    /// soft-delete preenchendo <c>DeletedBy</c>/<c>DeletedAt</c>.
    /// </summary>
    void Remover(LocalOferta localOferta);

    /// <summary>
    /// Indica se há algum local de oferta vivo que tem o campus informado como
    /// responsável — bloqueia a remoção do campus (handler de remoção de Campus).
    /// </summary>
    Task<bool> ExisteVivoComCampusResponsavelAsync(Guid campusResponsavelId, CancellationToken cancellationToken);

    /// <summary>
    /// Ponto de extensão (UNI-REQ-0010): indica se o local de oferta é
    /// referenciado por alguma oferta de curso viva — caso em que a remoção é
    /// bloqueada. A entidade <c>oferta_curso</c> ainda não existe no módulo, logo
    /// a implementação retorna <see langword="false"/>. Quando a oferta de curso
    /// chegar, esta checagem passa a consultá-la.
    /// </summary>
    Task<bool> ReferenciadoPorOfertaCursoVivaAsync(Guid localOfertaId, CancellationToken cancellationToken);
}
