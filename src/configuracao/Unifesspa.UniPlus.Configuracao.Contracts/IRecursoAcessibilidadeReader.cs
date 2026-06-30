namespace Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Leitor cross-módulo de <c>RecursoAcessibilidade</c> (ADR-0056). Expõe o estado
/// vivo do cadastro de recursos de acessibilidade para consumo por outros bounded
/// contexts (ex.: a configuração de atendimento especializado do Módulo Seleção,
/// que referencia o recurso ao montar um edital) sem acesso direto ao banco de
/// Configuração (ADR-0054).
/// </summary>
public interface IRecursoAcessibilidadeReader
{
    /// <summary>
    /// Lista todos os recursos de acessibilidade vivos (não soft-deleted),
    /// ordenados por <c>Nome</c> ascendente para determinismo cross-cliente.
    /// </summary>
    Task<IReadOnlyList<RecursoAcessibilidadeView>> ListarVivosAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém um recurso de acessibilidade pelo <paramref name="id"/>, ou
    /// <see langword="null"/> se inexistente / soft-deleted.
    /// </summary>
    Task<RecursoAcessibilidadeView?> ObterPorIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
