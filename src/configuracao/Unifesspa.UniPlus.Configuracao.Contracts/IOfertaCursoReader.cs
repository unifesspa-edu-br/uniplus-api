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

    /// <summary>
    /// Obtém a oferta de curso viva com o <paramref name="codigo"/>, ou <see langword="null"/> se inexistente, removida ou
    /// de formato inválido. O valor de busca é normalizado (<c>Trim</c> + NFC) antes da comparação, que é ordinal.
    /// </summary>
    /// <remarks>
    /// Existe para quem cita a oferta pelo código e precisa recusar a referência na escrita: o código é único entre as
    /// ofertas vivas, e uma referência que nunca vai bater tem de morrer no cadastro.
    /// </remarks>
    Task<OfertaCursoView?> ObterVivaPorCodigoAsync(
        string codigo,
        CancellationToken cancellationToken = default);
}
