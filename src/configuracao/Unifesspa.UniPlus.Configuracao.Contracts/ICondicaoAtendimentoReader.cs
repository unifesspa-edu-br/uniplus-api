namespace Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Leitor cross-módulo de <c>CondicaoAtendimentoEspecializado</c> (ADR-0056).
/// Expõe o estado vivo do cadastro de condições de atendimento especializado para
/// consumo por outros bounded contexts (ex.: a configuração de atendimento
/// especializado do Módulo Seleção, que referencia a condição ao montar as opções
/// de um edital) sem acesso direto ao banco de Configuração (ADR-0054).
/// </summary>
public interface ICondicaoAtendimentoReader
{
    /// <summary>
    /// Lista todas as condições vivas (não soft-deleted), ordenadas por
    /// <c>Codigo</c> ascendente para determinismo cross-cliente.
    /// </summary>
    Task<IReadOnlyList<CondicaoAtendimentoView>> ListarVivosAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém uma condição pelo <paramref name="id"/>, ou <see langword="null"/> se
    /// inexistente / soft-deleted.
    /// </summary>
    Task<CondicaoAtendimentoView?> ObterPorIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
