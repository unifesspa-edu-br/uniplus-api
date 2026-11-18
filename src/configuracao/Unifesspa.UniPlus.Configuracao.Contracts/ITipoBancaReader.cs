namespace Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Leitor cross-módulo de <c>TipoBanca</c> (ADR-0056). Expõe o estado vivo do
/// cadastro de tipos de banca para consumo por outros bounded contexts (ex.: o
/// Módulo Seleção ao configurar as bancas requeridas por fase de um processo) sem
/// acesso direto ao banco de Configuração (ADR-0054).
/// </summary>
public interface ITipoBancaReader
{
    /// <summary>
    /// Lista todos os tipos de banca vivos (não soft-deleted), ordenados por
    /// <c>Codigo</c> ascendente para determinismo cross-cliente.
    /// </summary>
    Task<IReadOnlyList<TipoBancaView>> ListarVivosAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Os tipos de banca vivos (não soft-deleted) dos <paramref name="codigos"/>, numa só
    /// leitura, ordenados por <c>Codigo</c> ascendente. O código sem tipo vivo — inexistente,
    /// fora do formato ou removido — não aparece no resultado, e um código repetido no lote
    /// devolve o tipo uma só vez.
    /// </summary>
    Task<IReadOnlyList<TipoBancaView>> ListarVivosPorCodigosAsync(
        IReadOnlyCollection<string> codigos,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém um tipo de banca pelo <paramref name="id"/>, ou <see langword="null"/>
    /// se inexistente / soft-deleted.
    /// </summary>
    Task<TipoBancaView?> ObterPorIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
