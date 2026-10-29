namespace Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>Repositório dos perfis de acesso.</summary>
public interface IPerfilRepository : IRepository<Perfil>
{
    /// <summary>
    /// Lista os perfis paginados por cursor keyset bidirecional (ADR-0026 + ADR-0089),
    /// ordenados por <c>Id</c> (Guid v7 cronológico), sem rastreamento.
    /// </summary>
    Task<(IReadOnlyList<Perfil> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lista os perfis ativos, o que se pode pedir, paginados por cursor keyset bidirecional (ADR-0026 + ADR-0089),
    /// sem rastreamento.
    /// </summary>
    Task<(IReadOnlyList<Perfil> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarAtivosPaginadoAsync(
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lê o perfil a editar travando-o com <c>FOR UPDATE</c> até o fim da transação: a concessão
    /// simultânea, que o lê com <c>FOR SHARE</c>, espera a edição ou a precede, e a conferência de
    /// que o perfil já tem concessões enxerga todas as que foram confirmadas.
    /// </summary>
    Task<Perfil?> ObterParaEditarAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Indica se outro perfil já usa o nome, sem distinguir maiúsculas de minúsculas. A checagem
    /// antecede a gravação para devolver <c>409</c> em vez da violação crua do índice único; a
    /// corrida remanescente é coberta pela tradução da violação.
    /// </summary>
    /// <param name="exceto">Perfil que não conta, na edição do próprio nome.</param>
    Task<bool> NomeExisteAsync(string nome, Guid? exceto, CancellationToken cancellationToken = default);
}
