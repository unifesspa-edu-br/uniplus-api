namespace Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Leitor cross-módulo de <c>CategoriaDocumento</c> (ADR-0056). Expõe o estado vivo
/// do cadastro classificatório de categorias de documento para consumo por outros
/// bounded contexts (ex.: o Módulo Seleção ao declarar de quais categorias cada banca
/// requerida por uma fase é competente) sem acesso direto ao banco de Configuração
/// (ADR-0054).
/// </summary>
public interface ICategoriaDocumentoReader
{
    /// <summary>
    /// Lista todas as categorias vivas (não soft-deleted), ordenadas por
    /// <c>Codigo</c> ascendente para determinismo cross-cliente. A <c>Ordem</c> do
    /// cadastro é critério de exibição e não serve como ordem de leitura: ela admite
    /// empate, e um empate faria a mesma consulta devolver sequências distintas.
    /// </summary>
    Task<IReadOnlyList<CategoriaDocumentoView>> ListarVivosAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// As categorias vivas (não soft-deleted) dos <paramref name="codigos"/>, numa só
    /// leitura, ordenadas por <c>Codigo</c> ascendente. O código sem categoria viva —
    /// inexistente, fora do formato ou removida — não aparece no resultado, e um código
    /// repetido no lote devolve a categoria uma só vez.
    /// </summary>
    Task<IReadOnlyList<CategoriaDocumentoView>> ListarVivasPorCodigosAsync(
        IReadOnlyCollection<string> codigos,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém uma categoria pelo <paramref name="id"/>, ou <see langword="null"/> se
    /// inexistente / soft-deleted.
    /// </summary>
    Task<CategoriaDocumentoView?> ObterPorIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
