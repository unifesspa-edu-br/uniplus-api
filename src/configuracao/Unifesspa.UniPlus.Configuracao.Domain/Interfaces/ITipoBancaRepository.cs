namespace Unifesspa.UniPlus.Configuracao.Domain.Interfaces;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Repositório da entidade <see cref="TipoBanca"/> (ADR-0054: banco isolado
/// <c>uniplus_configuracao</c>). Todas as leituras excluem registros soft-deleted
/// via query filter por convenção. Não há métodos de integridade referencial: o
/// tipo de banca não é referenciado por FK intra-banco (o consumo cross-módulo é
/// por snapshot-copy desacoplado, ADR-0061).
/// </summary>
public interface ITipoBancaRepository
{
    /// <summary>Carrega o tipo de banca rastreado pelo contexto, para mutação.</summary>
    Task<TipoBanca?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Carrega o tipo de banca para leitura (<c>AsNoTracking</c>) — projeção em DTO.</summary>
    Task<TipoBanca?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Lista tipos de banca vivos paginados por cursor keyset bidirecional (ADR-0026
    /// + ADR-0089): ordena por <c>Id</c> (Guid v7, ADR-0032) e devolve as âncoras de
    /// <c>prev</c>/<c>next</c> (nulas quando não há aquele lado).
    /// </summary>
    Task<(IReadOnlyList<TipoBanca> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken);

    Task AdicionarAsync(TipoBanca banca, CancellationToken cancellationToken);

    /// <summary>
    /// Marca o tipo de banca para remoção; o <c>SoftDeleteInterceptor</c> converte
    /// em soft-delete preenchendo <c>DeletedBy</c>/<c>DeletedAt</c>.
    /// </summary>
    void Remover(TipoBanca banca);

    /// <summary>
    /// Verifica se existe tipo de banca vivo com o <paramref name="codigo"/>
    /// (case-sensitive, sobre o valor normalizado por <c>Trim</c>), excluindo
    /// opcionalmente um <paramref name="excluirId"/> (para a checagem na atualização).
    /// </summary>
    Task<bool> CodigoExisteEntreVivosAsync(
        string codigo,
        Guid? excluirId,
        CancellationToken cancellationToken);
}
