namespace Unifesspa.UniPlus.Governance.Contracts;

/// <summary>
/// Leitor cross-módulo de <c>Unidade</c> (ADR-0056). Expõe o estado vivo da
/// Unidade para consumo por outros bounded contexts (ex.: Seleção, Configuração)
/// sem acesso direto ao banco de Organização (ADR-0054).
/// </summary>
/// <remarks>
/// A implementação canônica é respaldada por cache Redis (TTL ~5 min) com
/// stampede protection (ADR-0057 Pattern 4). Cada API que precisa de Unidade
/// hospeda sua própria instância via
/// <c>AddOrganizacaoInstitucionalInfrastructure()</c>.
/// </remarks>
public interface IUnidadeReader
{
    /// <summary>
    /// Lista todas as Unidades ativas (não soft-deleted), ordenadas por sigla
    /// ascendente para determinismo cross-cliente.
    /// </summary>
    Task<IReadOnlyList<UnidadeView>> ListarAtivasAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém uma Unidade pelo <paramref name="id"/>, ou
    /// <see langword="null"/> se inexistente / soft-deleted.
    /// </summary>
    Task<UnidadeView?> ObterPorIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
