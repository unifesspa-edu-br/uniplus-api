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

    /// <summary>
    /// Os tipos de deficiência vivos (não soft-deleted) dos <paramref name="codigos"/>, numa só leitura, ordenados por
    /// <c>Codigo</c> ascendente. O código sem cadastro vivo — fora do formato, inexistente ou removido — não aparece
    /// no resultado, e um código repetido no lote devolve o cadastro uma só vez. A comparação usa os códigos exatos,
    /// sem aparar nem normalizar.
    /// </summary>
    Task<IReadOnlyList<TipoDeficienciaView>> ListarVivosPorCodigosAsync(
        IReadOnlyCollection<string> codigos,
        CancellationToken cancellationToken = default);
}
