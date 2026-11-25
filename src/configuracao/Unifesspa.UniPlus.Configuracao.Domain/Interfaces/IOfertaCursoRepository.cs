namespace Unifesspa.UniPlus.Configuracao.Domain.Interfaces;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Repositório da entidade <see cref="OfertaCurso"/> (ADR-0054: banco isolado
/// <c>uniplus_configuracao</c>). Todas as leituras excluem registros
/// soft-deleted via query filter por convenção. O código da oferta é único
/// entre as vivas.
/// </summary>
public interface IOfertaCursoRepository
{
    /// <summary>Carrega a oferta rastreada pelo contexto, para mutação.</summary>
    Task<OfertaCurso?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Verifica se existe oferta viva com o <paramref name="codigo"/> (com sensibilidade a caixa, sobre o valor normalizado por
    /// <c>Trim</c>), excluindo opcionalmente <paramref name="excluirId"/> — a própria oferta, na edição.
    /// </summary>
    Task<bool> CodigoExisteEntreVivosAsync(string codigo, Guid? excluirId, CancellationToken cancellationToken);

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
    /// <param name="afterSortKey">
    /// Chave de ordenação da âncora de continuação; <c>null</c> na primeira página.
    /// </param>
    Task<(IReadOnlyList<OfertaCurso> Itens, (string SortKey, Guid Id)? Anterior, (string SortKey, Guid Id)? Proximo)>
        ListarPaginadoAsync(
            IReadOnlyList<SortField> ordenacao,
            FiltroDasOfertasDeCurso filtro,
            string? afterSortKey,
            Guid? afterId,
            int limit,
            PaginationDirection direction,
            CancellationToken cancellationToken);

    /// <summary>Conta as ofertas do mesmo filtro usado na paginação.</summary>
    Task<int> ContarAsync(FiltroDasOfertasDeCurso filtro, CancellationToken cancellationToken);

    Task AdicionarAsync(OfertaCurso ofertaCurso, CancellationToken cancellationToken);

    /// <summary>
    /// Marca a oferta para remoção; o <c>SoftDeleteInterceptor</c> converte em
    /// soft-delete preenchendo <c>DeletedBy</c>/<c>DeletedAt</c>.
    /// </summary>
    void Remover(OfertaCurso ofertaCurso);
}
