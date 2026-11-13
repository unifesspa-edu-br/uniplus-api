namespace Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>A concessão alvo de uma providência, com o nome do perfil que ela concede, para a resposta e os avisos.</summary>
public sealed record ConcessaoDaProvidencia(Concessao Concessao, string PerfilNome);

/// <summary>Providência lida junto da concessão alvo, para a resposta.</summary>
public sealed record ProvidenciaComConcessao(Providencia Providencia, ConcessaoDaProvidencia Alvo) : IIdentificavel
{
    /// <summary>Id da providência, exposto para a paginação keyset.</summary>
    public Guid Id { get; init; } = Providencia.Id;
}

/// <summary>
/// O filtro da fila das providências: a situação pedida, que a leitura decide no instante informado, e opcionalmente a
/// concessão. Sem situação, todas.
/// </summary>
public sealed record FiltroDeProvidencias(DateTimeOffset Agora, SituacaoDaProvidencia? Situacao, Guid? ConcessaoId = null);

/// <summary>
/// Repositório das providências sobre concessões. <see cref="IRepository{T}.ObterPorIdAsync"/> devolve a providência
/// rastreada, com o <c>xmin</c> lido, e é a leitura de quem a altera, sempre depois da trava da concessão.
/// </summary>
public interface IProvidenciaRepository : IRepository<Providencia>
{
    /// <summary>
    /// A concessão da providência, sem trava e sem rastreamento: só para saber que concessão travar, o que não muda. Nula
    /// quando a providência não existe.
    /// </summary>
    Task<Guid?> ObterConcessaoIdAsync(Guid providenciaId, CancellationToken cancellationToken = default);

    /// <summary>
    /// A concessão com o nome do perfil, lida depois de travada com <c>FOR SHARE</c> até o fim da transação, sem
    /// rastreamento: a revogação concorrente, por qualquer caminho, espera ou é esperada, e a leitura enxerga a revogação
    /// já confirmada. Nula quando a concessão não existe.
    /// </summary>
    Task<ConcessaoDaProvidencia?> ObterConcessaoSobTravaCompartilhadaAsync(Guid concessaoId, CancellationToken cancellationToken = default);

    /// <summary>A providência ainda aberta sobre a concessão, sem rastreamento; nula quando não há.</summary>
    Task<Providencia?> ObterAbertaDaConcessaoAsync(Guid concessaoId, CancellationToken cancellationToken = default);

    /// <summary>A providência com a concessão alvo, sem rastreamento.</summary>
    Task<ProvidenciaComConcessao?> ObterComConcessaoAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// As providências paginadas por cursor keyset bidirecional (ADR-0026 + ADR-0089), em ordem de abertura, a mais antiga
    /// primeiro, com a situação filtrada no banco pelo mesmo critério de <see cref="Concessao.EstaAtiva"/>.
    /// </summary>
    Task<(IReadOnlyList<ProvidenciaComConcessao> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        FiltroDeProvidencias filtro,
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken = default);

    /// <summary>As providências da concessão, sem rastreamento, para a trilha dela.</summary>
    Task<IReadOnlyList<Providencia>> ListarDaConcessaoAsync(Guid concessaoId, CancellationToken cancellationToken = default);
}
