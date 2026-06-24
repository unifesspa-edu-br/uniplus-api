namespace Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Leitor cross-módulo de <c>PesoAreaEnem</c> (ADR-0056). Expõe o estado vivo dos
/// pesos do ENEM por grupo de área para consumo por outros bounded contexts
/// (ex.: a frente de classificação do Módulo Seleção, que resolve o grupo de área
/// de cada curso contra a resolução escolhida) sem acesso direto ao banco de
/// Configuração (ADR-0054).
/// </summary>
public interface IPesoAreaEnemReader
{
    /// <summary>
    /// Lista todas as linhas de pesos vivas (não soft-deleted), ordenadas por
    /// <c>Resolucao</c> e <c>GrupoCurso</c> ascendentes para determinismo cross-cliente.
    /// </summary>
    Task<IReadOnlyList<PesoAreaEnemView>> ListarVivasAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém uma linha de pesos pelo <paramref name="id"/>, ou
    /// <see langword="null"/> se inexistente / soft-deleted.
    /// </summary>
    Task<PesoAreaEnemView?> ObterPorIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
