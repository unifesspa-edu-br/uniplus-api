namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Abstractions;

/// <summary>
/// Abstração consumida pelos handlers de comando para invalidar a entrada de
/// cache do <c>IAreaOrganizacionalReader</c> após mutação. Mantém a regra
/// "Application não depende de <c>ICacheService</c>" da Clean Architecture
/// (ADR-0042); a implementação em <c>OrganizacaoInstitucional.Infrastructure</c>
/// detém o conhecimento da chave Redis canônica.
/// </summary>
/// <remarks>
/// Invocação é <strong>best-effort</strong> e <strong>pós-commit</strong>:
/// chamado após <c>IUnitOfWork.SalvarAlteracoesAsync</c>. Se a invalidação
/// falhar (Redis indisponível), o cache stale expira pelo TTL natural (5 min)
/// — escolha consciente em vez de tornar o cache parte da transação EF, o que
/// criaria distributed transaction sem ganho proporcional ao risco.
/// </remarks>
public interface IAreaOrganizacionalCacheInvalidator
{
    /// <summary>
    /// Remove a entrada de cache canônica do reader. Idempotente. Não lança
    /// em caso de falha de infra — implementação loga warning.
    /// </summary>
    Task InvalidarAsync(CancellationToken cancellationToken = default);
}
