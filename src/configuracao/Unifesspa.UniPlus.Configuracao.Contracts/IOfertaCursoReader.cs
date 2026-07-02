namespace Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Leitor cross-módulo de <c>OfertaCurso</c> (ADR-0056). Expõe o estado vivo do
/// cadastro de ofertas para consumo por outros bounded contexts (ex.: o quadro de
/// vagas de um edital no Módulo Seleção) sem acesso direto ao schema de
/// Configuração (ADR-0054/0097).
/// </summary>
public interface IOfertaCursoReader
{
    /// <summary>
    /// Lista todas as ofertas de curso vivas (não soft-deleted), ordenadas por
    /// <c>Id</c> ascendente para determinismo cross-cliente.
    /// </summary>
    Task<IReadOnlyList<OfertaCursoView>> ListarVivasAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém uma oferta de curso pelo <paramref name="id"/>, ou
    /// <see langword="null"/> se inexistente / soft-deleted.
    /// </summary>
    Task<OfertaCursoView?> ObterPorIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
