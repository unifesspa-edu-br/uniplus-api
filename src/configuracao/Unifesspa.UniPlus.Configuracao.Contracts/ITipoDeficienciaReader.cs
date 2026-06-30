namespace Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Leitor cross-módulo de <c>TipoDeficiencia</c> (ADR-0056). Expõe o estado vivo do
/// cadastro classificatório de tipos de deficiência para consumo por outros bounded
/// contexts (ex.: a solicitação de atendimento especializado do Módulo Seleção) sem
/// acesso direto ao banco de Configuração (ADR-0054).
/// </summary>
public interface ITipoDeficienciaReader
{
    /// <summary>
    /// Lista todos os tipos de deficiência vivos (não soft-deleted), ordenados por
    /// <c>Nome</c> ascendente para determinismo cross-cliente.
    /// </summary>
    Task<IReadOnlyList<TipoDeficienciaView>> ListarVivosAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém um tipo de deficiência pelo <paramref name="id"/>, ou
    /// <see langword="null"/> se inexistente / soft-deleted.
    /// </summary>
    Task<TipoDeficienciaView?> ObterPorIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
