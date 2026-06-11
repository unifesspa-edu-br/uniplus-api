namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Interfaces;

using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;

/// <summary>
/// Repositório da entidade singleton <see cref="Instituicao"/> (ADR-0054: banco
/// isolado de Organização). Todas as leituras excluem registros soft-deleted via
/// query filter do EF Core, de modo que "viva" e "presente nas leituras padrão"
/// são equivalentes.
/// </summary>
public interface IInstituicaoRepository
{
    /// <summary>
    /// Carrega a Instituição pelo Id, rastreada pelo contexto — para mutação
    /// (atualização/remoção).
    /// </summary>
    Task<Instituicao?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Carrega a Instituição viva (singleton) para leitura (<c>AsNoTracking</c>),
    /// ou <see langword="null"/> se nenhuma existe — para projeção em DTO/View.
    /// </summary>
    Task<Instituicao?> ObterParaLeituraAsync(CancellationToken cancellationToken);

    Task AdicionarAsync(Instituicao instituicao, CancellationToken cancellationToken);

    /// <summary>
    /// Marca a Instituição para remoção no contexto EF. O
    /// <c>SoftDeleteInterceptor</c> converte automaticamente para soft-delete
    /// preenchendo <c>DeletedBy</c>/<c>DeletedAt</c> a partir do
    /// <c>IUserContext</c> e do <c>TimeProvider</c>.
    /// </summary>
    void Remover(Instituicao instituicao);

    /// <summary>
    /// Indica se já existe alguma Instituição viva — guard de domínio do
    /// invariante singleton, consultado na criação (ADR-0055).
    /// </summary>
    Task<bool> ExisteAlgumaVivaAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Indica se alguma Instituição viva referencia <paramref name="unidadeId"/>
    /// como unidade raiz — usado para bloquear a remoção de uma Unidade que é a
    /// reitoria vinculada à Instituição.
    /// </summary>
    Task<bool> ExisteComUnidadeRaizAsync(Guid unidadeId, CancellationToken cancellationToken);
}
