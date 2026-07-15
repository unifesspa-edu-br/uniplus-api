namespace Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Leitor cross-módulo de <c>PrecedenciaFase</c> (ADR-0056). Expõe o grafo de
/// precedências vivo entre fases canônicas para consumo por outros bounded
/// contexts (ex.: o Módulo Seleção ao validar o cronograma de um processo) sem
/// acesso direto ao banco de Configuração (ADR-0054). Acrescentar uma aresta ao
/// cadastro muda o veredicto do consumidor sem recompilar.
/// </summary>
public interface IPrecedenciaFaseReader
{
    /// <summary>
    /// Lista todas as arestas vivas (não soft-deleted) do grafo de precedências,
    /// sem ordenação semântica específica — o consumidor monta o grafo a partir do
    /// conjunto completo.
    /// </summary>
    Task<IReadOnlyList<PrecedenciaFaseView>> ListarVivasAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém uma aresta pelo <paramref name="id"/>, ou <see langword="null"/> se
    /// inexistente / soft-deleted.
    /// </summary>
    Task<PrecedenciaFaseView?> ObterPorIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
