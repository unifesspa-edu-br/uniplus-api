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

    /// <summary>
    /// Obtém a Unidade viva que tem hoje o <paramref name="codigo"/>, ou <see langword="null"/> se nenhuma tem:
    /// código que a Unidade já teve e perdeu, ou de Unidade excluída, não resolve. Lê direto do banco, sem o cache
    /// de <see cref="ListarAtivasAsync"/>, porque quem resolve por código congela o resultado e não pode congelar
    /// uma Unidade que o cache ainda mostra com o código antigo.
    /// </summary>
    Task<UnidadeView?> ObterPorCodigoAsync(
        string codigo,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// As Unidades vivas que têm hoje cada um dos <paramref name="codigos"/>, numa só leitura direto do banco, pela mesma razão de
    /// <see cref="ObterPorCodigoAsync"/>, ordenadas por sigla. O código que nenhuma Unidade viva tem não aparece no resultado, e
    /// um código repetido no lote devolve a Unidade uma só vez.
    /// </summary>
    Task<IReadOnlyList<UnidadeView>> ListarPorCodigosAsync(
        IReadOnlyCollection<string> codigos,
        CancellationToken cancellationToken = default);
}
