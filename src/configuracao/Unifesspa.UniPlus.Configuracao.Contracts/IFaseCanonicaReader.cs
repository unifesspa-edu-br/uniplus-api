namespace Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Leitor cross-módulo de <c>FaseCanonica</c> (ADR-0056). Expõe o estado vivo do
/// cadastro de fases para consumo por outros bounded contexts (ex.: o Módulo
/// Seleção ao montar o cronograma de um processo) sem acesso direto ao banco de
/// Configuração (ADR-0054).
/// </summary>
public interface IFaseCanonicaReader
{
    /// <summary>
    /// Lista todas as fases vivas (não soft-deleted), ordenadas por <c>Codigo</c>
    /// ascendente para determinismo cross-cliente.
    /// </summary>
    Task<IReadOnlyList<FaseCanonicaView>> ListarVivosAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém uma fase pelo <paramref name="id"/>, ou <see langword="null"/> se
    /// inexistente / soft-deleted.
    /// </summary>
    Task<FaseCanonicaView?> ObterPorIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
