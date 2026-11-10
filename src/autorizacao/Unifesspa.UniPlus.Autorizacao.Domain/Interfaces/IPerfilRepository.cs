namespace Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
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
    /// Lista os perfis que se podem pedir, paginados por cursor keyset bidirecional (ADR-0026 + ADR-0089), sem
    /// rastreamento: os ativos que são de governança ou têm uma versão em vigor. O filtro vem antes da paginação.
    /// </summary>
    Task<(IReadOnlyList<Perfil> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPediveisPaginadoAsync(
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Os perfis com versão pendente que ainda vale em <paramref name="agora"/>, com versões e vigências, sem rastrear
    /// nada: a leitura das filas de aprovação, que calculam a classe e a etapa pelo catálogo atual.
    /// </summary>
    Task<IReadOnlyList<Perfil>> ListarComPendenteNoPrazoAsync(DateTimeOffset agora, CancellationToken cancellationToken = default);

    /// <summary>
    /// Os identificadores dos perfis cuja versão pendente já passou do prazo em <paramref name="agora"/>, lidos sem
    /// trava: quem os materializa trava e confere cada perfil de novo.
    /// </summary>
    Task<IReadOnlyList<Guid>> ListarComPendenteVencidaAsync(DateTimeOffset agora, CancellationToken cancellationToken = default);

    /// <summary>O perfil dono do documento da versão, sem rastrear nada; nulo quando o documento não existe.</summary>
    Task<Guid?> ObterPerfilIdDoDocumentoAsync(Guid documentoId, CancellationToken cancellationToken = default);

    /// <summary>Acrescenta o registro de acesso ao documento da versão, só acrescentado.</summary>
    Task AdicionarLeituraAsync(LeituraDoDocumentoDaVersao leitura, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lê o perfil, com versões, documentos e vigências, travando-o com <c>FOR UPDATE</c> até o fim da transação: a
    /// concessão simultânea, que o lê com <c>FOR SHARE</c>, espera a operação ou a precede, e a conferência de que o
    /// perfil já tem concessões enxerga todas as que foram confirmadas. Toda operação sobre versão ou documento começa
    /// por aqui.
    /// </summary>
    Task<Perfil?> ObterParaEditarAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// A trilha da contenção do perfil, sem rastrear nada, com as suspensões e as desativações já fechadas; nula quando o
    /// perfil não existe.
    /// </summary>
    Task<TrilhaDaContencao?> ObterTrilhaDaContencaoAsync(Guid perfilId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Indica se outro perfil já usa o nome, sem distinguir maiúsculas de minúsculas. A checagem
    /// antecede a gravação para devolver <c>409</c> em vez da violação crua do índice único; a
    /// corrida remanescente é coberta pela tradução da violação.
    /// </summary>
    /// <param name="exceto">Perfil que não conta, na edição do próprio nome.</param>
    Task<bool> NomeExisteAsync(string nome, Guid? exceto, CancellationToken cancellationToken = default);
}
