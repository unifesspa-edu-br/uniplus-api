namespace Unifesspa.UniPlus.Selecao.Domain.Interfaces;

using Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Repositório único do agregado <see cref="ProcessoSeletivo"/>: carrega e
/// persiste a raiz com as entidades de configuração já modeladas (etapas e
/// oferta de atendimento especializado; as demais dimensões entram nas fatias
/// seguintes). Nenhuma entidade filha tem repositório próprio.
/// </summary>
public interface IProcessoSeletivoRepository : IRepository<ProcessoSeletivo>
{
    /// <summary>
    /// Obtém o processo com toda a configuração carregada (todas as coleções
    /// filhas, inclusive as filhas da oferta de atendimento). É a forma
    /// canônica de materializar o agregado para os comandos <c>Definir*</c> e
    /// para a consulta de conformidade.
    /// </summary>
    Task<ProcessoSeletivo?> ObterComConfiguracaoAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lista processos paginados por cursor keyset bidirecional (ADR-0026 +
    /// ADR-0089): ordena por <c>Id</c> e retorna até <paramref name="limit"/>
    /// itens na direção <paramref name="direction"/> a partir de
    /// <paramref name="afterId"/> (ou a primeira janela quando <c>null</c>),
    /// sempre em ordem ascendente, junto das âncoras de <c>prev</c>/<c>next</c>
    /// (nulas quando não há aquele lado). Implementações aplicam <c>AsNoTracking</c>.
    /// </summary>
    Task<(IReadOnlyList<ProcessoSeletivo> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adiciona o <see cref="SnapshotPublicacao"/> congelado por
    /// <see cref="ProcessoSeletivo.Publicar"/> (Story #759, T4 #785). Sem
    /// repositório próprio para a entidade forense — a issue #759 §4
    /// estabelece que <see cref="IProcessoSeletivoRepository"/> persiste o
    /// agregado inteiro, incluindo <see cref="Edital"/> e <see cref="SnapshotPublicacao"/>.
    /// </summary>
    Task AdicionarSnapshotPublicacaoAsync(SnapshotPublicacao snapshot, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolve a publicação vigente num instante (RN08, Story #759 T6 #787,
    /// ADR-0075/0076): o <see cref="Edital"/> publicado (<c>DataPublicacao</c>
    /// não nula) de MAIOR data ≤ <paramref name="instante"/> e o seu
    /// <see cref="SnapshotPublicacao"/>. <see langword="null"/> quando não há
    /// Edital publicado ≤ o instante (inclusive processo inexistente ou ainda
    /// em rascunho). O empate é impossível por
    /// <c>ux_editais_processo_data_publicacao</c>. Leitura <c>AsNoTracking</c>.
    /// </summary>
    Task<(Edital Edital, SnapshotPublicacao Snapshot)?> ObterSnapshotVigenteAsync(
        Guid processoSeletivoId,
        DateTimeOffset instante,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// <see langword="true"/> se existe um Processo Seletivo com este id
    /// (checagem barata via <c>AnyAsync</c>, sem materializar o agregado). Usada
    /// pelo seletor de snapshot vigente para distinguir 404 (processo
    /// inexistente) de 422 (sem publicação vigente ≤ o instante).
    /// </summary>
    Task<bool> ExisteAsync(Guid id, CancellationToken cancellationToken = default);
}
