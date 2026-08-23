namespace Unifesspa.UniPlus.Selecao.Domain.Interfaces;

using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Repositório do catálogo de motivos de decisão de isenção (UNI-REQ-0120).
/// </summary>
public interface IMotivoDecisaoIsencaoRepository : IRepository<MotivoDecisaoIsencao>
{
    /// <summary>
    /// Lista motivos paginados por cursor keyset bidirecional (ADR-0026 +
    /// ADR-0089), com filtro opcional por fundamento e por situação. Ordenação
    /// estável por <c>Id</c> (Guid v7 cronológico).
    /// </summary>
    /// <param name="apenasAtivos">
    /// Quando verdadeiro, devolve só os motivos que ainda entram em novas
    /// publicações. Quem monta uma publicação usa esta visão; quem administra o
    /// catálogo precisa enxergar os desativados para reativá-los.
    /// </param>
    Task<(IReadOnlyList<MotivoDecisaoIsencao> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        FundamentoIsencao? fundamento,
        bool apenasAtivos,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Indica se o código já pertence a algum motivo. A checagem antecede a
    /// inserção para devolver <c>409</c> em vez da violação crua do índice
    /// único; a corrida remanescente é coberta pela tradução da violação.
    /// </summary>
    /// <remarks>
    /// A unicidade alcança inclusive os motivos desativados: o código é citado
    /// nas decisões já proferidas, e reaproveitá-lo em um motivo novo faria duas
    /// coisas diferentes responderem pelo mesmo rótulo na leitura do histórico.
    /// </remarks>
    Task<bool> CodigoExisteAsync(
        string codigo,
        CancellationToken cancellationToken = default);
}
