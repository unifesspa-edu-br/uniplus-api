namespace Unifesspa.UniPlus.Configuracao.Domain.Interfaces;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Repositório da entidade <see cref="TermoConsentimento"/> (ADR-0054: banco isolado
/// <c>uniplus_configuracao</c>). Todas as leituras excluem registros soft-deleted via
/// query filter por convenção.
/// </summary>
public interface ITermoConsentimentoRepository
{
    /// <summary>Carrega o termo rastreado pelo contexto (com <see cref="TermoConsentimento.Versoes"/>), para mutação.</summary>
    Task<TermoConsentimento?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Carrega o termo para leitura (<c>AsNoTracking</c>) — projeção em DTO.</summary>
    Task<TermoConsentimento?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Lista termos vivos paginados por cursor keyset bidirecional (ADR-0026 +
    /// ADR-0089): ordena por <c>Id</c> (Guid v7, ADR-0032) e devolve as âncoras
    /// de <c>prev</c>/<c>next</c> (nulas quando não há aquele lado). O filtro de
    /// <paramref name="busca"/>, quando informado, é aplicado ANTES do keyset —
    /// busca e paginação descrevem o mesmo conjunto de resultados (issue #1105).
    /// </summary>
    Task<(IReadOnlyList<TermoConsentimento> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        string? busca,
        CancellationToken cancellationToken);

    Task AdicionarAsync(TermoConsentimento termo, CancellationToken cancellationToken);

    /// <summary>
    /// Adiciona explicitamente a versão promovida ao <c>DbSet</c> — o EF Core não
    /// detecta como <c>Added</c> uma entidade só inserida na coleção em memória
    /// de um agregado já rastreado (recarregado do banco); ver
    /// <see cref="TermoConsentimento.Promover"/>. Também força um <c>UPDATE</c> do
    /// <paramref name="termo"/> amarrado ao token de concorrência otimista lido na
    /// consulta — sem esse write explícito, uma promoção concorrente com uma
    /// edição de rascunho que reverte a revisão não colidiria, e a versão imutável
    /// sairia gravada a partir de um rascunho já invalidado.
    /// </summary>
    Task AdicionarVersaoAsync(TermoConsentimento termo, TermoConsentimentoVersao versao, CancellationToken cancellationToken);

    /// <summary>
    /// Marca o termo para remoção; o <c>SoftDeleteInterceptor</c> converte em
    /// soft-delete preenchendo <c>DeletedBy</c>/<c>DeletedAt</c>.
    /// </summary>
    void Remover(TermoConsentimento termo);
}
