namespace Unifesspa.UniPlus.Governance.Contracts;

/// <summary>
/// Leitor cross-módulo de <c>AreaOrganizacional</c> (ADR-0055/0056). Cada API
/// hospeda sua própria instância via <c>AddOrganizacaoInstitucionalInfrastructure()</c>;
/// a implementação canônica é respaldada por cache Redis (TTL ~5 min) com
/// stampede protection (ADR-0057 Pattern 4).
/// </summary>
/// <remarks>
/// <para>
/// Esta interface <strong>não</strong> recebe <c>IReadOnlyCollection&lt;AreaCodigo&gt; areasCaller</c>
/// como os readers de entidades área-scoped (<c>IModalidadeReader</c>, …) porque
/// <c>AreaOrganizacional</c> NÃO é <see cref="IAreaScopedEntity"/>: ela é a própria
/// dimensão de governança, visível a todos os módulos por design (ADR-0055
/// §"Acesso cross-módulo"). É a exceção explícita ao fitness test R-readers
/// da ADR-0056.
/// </para>
/// <para>
/// As views retornadas refletem apenas áreas <strong>ativas</strong> (não
/// soft-deleted). Áreas removidas continuam referenciáveis para auditoria
/// histórica via Domain/Infrastructure, mas não aparecem em listagens read-side.
/// </para>
/// </remarks>
public interface IAreaOrganizacionalReader
{
    /// <summary>
    /// Lista todas as áreas organizacionais ativas (não soft-deleted), ordenadas
    /// por código ascendente para determinismo de exibição cross-cliente.
    /// </summary>
    Task<IReadOnlyList<AreaOrganizacionalView>> ListarAtivasAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém uma área pelo seu <see cref="AreaCodigo"/>, ou <see langword="null"/>
    /// se inexistente / soft-deleted.
    /// </summary>
    Task<AreaOrganizacionalView?> ObterPorCodigoAsync(
        AreaCodigo codigo,
        CancellationToken cancellationToken = default);
}
