namespace Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Leitor cross-módulo de <c>ReferenciaReservaDemografica</c> (ADR-0056). Expõe
/// o estado vivo das referências de reserva demográfica para consumo por outros
/// bounded contexts (ex.: a frente de distribuição de vagas do Módulo Seleção)
/// sem acesso direto ao banco de Configuração (ADR-0054).
/// </summary>
public interface IReferenciaReservaDemograficaReader
{
    /// <summary>
    /// Lista todas as referências vivas (não soft-deleted), ordenadas por
    /// <c>CensoReferencia</c> ascendente para determinismo cross-cliente.
    /// </summary>
    Task<IReadOnlyList<ReferenciaReservaDemograficaView>> ListarVivasAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém uma referência pelo <paramref name="id"/>, ou
    /// <see langword="null"/> se inexistente / soft-deleted.
    /// </summary>
    Task<ReferenciaReservaDemograficaView?> ObterPorIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
