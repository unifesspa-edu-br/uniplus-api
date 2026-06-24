namespace Unifesspa.UniPlus.Configuracao.Domain.Interfaces;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Repositório da entidade <see cref="PesoAreaEnem"/> (ADR-0054: banco isolado
/// <c>uniplus_configuracao</c>). Todas as leituras excluem registros
/// soft-deleted via query filter por convenção.
/// </summary>
public interface IPesoAreaEnemRepository
{
    /// <summary>Carrega a linha de pesos rastreada pelo contexto, para mutação.</summary>
    Task<PesoAreaEnem?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Carrega a linha de pesos para leitura (<c>AsNoTracking</c>) — projeção em DTO.</summary>
    Task<PesoAreaEnem?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Lista linhas de pesos vivas paginadas por cursor keyset bidirecional
    /// (ADR-0026 + ADR-0089): ordena por <c>Id</c> (Guid v7, ADR-0032) e devolve
    /// as âncoras de <c>prev</c>/<c>next</c> (nulas quando não há aquele lado).
    /// </summary>
    Task<(IReadOnlyList<PesoAreaEnem> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken);

    Task AdicionarAsync(PesoAreaEnem peso, CancellationToken cancellationToken);

    /// <summary>
    /// Marca a linha de pesos para remoção; o <c>SoftDeleteInterceptor</c> converte
    /// em soft-delete preenchendo <c>DeletedBy</c>/<c>DeletedAt</c>.
    /// </summary>
    void Remover(PesoAreaEnem peso);

    /// <summary>
    /// Verifica se existe linha viva para o par (resolução, grupo de curso),
    /// excluindo opcionalmente um <paramref name="excluirId"/>.
    /// </summary>
    Task<bool> ParExisteEntreVivosAsync(
        string resolucao,
        string grupoCurso,
        Guid? excluirId,
        CancellationToken cancellationToken);
}
