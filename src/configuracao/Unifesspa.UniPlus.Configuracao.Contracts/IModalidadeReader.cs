namespace Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Leitor cross-módulo de <c>Modalidade</c> de concorrência (ADR-0056). Expõe o
/// estado vivo do cadastro de modalidades para consumo por outros bounded contexts
/// (ex.: a configuração de modalidades de um edital no Módulo Seleção) sem acesso
/// direto ao banco de Configuração (ADR-0054).
/// </summary>
public interface IModalidadeReader
{
    /// <summary>
    /// Lista todas as modalidades vivas (não soft-deleted), ordenadas por
    /// <c>Codigo</c> ascendente para determinismo cross-cliente.
    /// </summary>
    Task<IReadOnlyList<ModalidadeView>> ListarVivosAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém uma modalidade pelo <paramref name="id"/>, ou <see langword="null"/>
    /// se inexistente / soft-deleted.
    /// </summary>
    Task<ModalidadeView?> ObterPorIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
