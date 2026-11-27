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

    /// <summary>
    /// Os recursos de acessibilidade vivos (não soft-deleted) dos <paramref name="codigos"/>, numa só leitura, ordenados por
    /// <c>Codigo</c> ascendente. O código sem cadastro vivo — fora do formato, inexistente ou removido — não aparece
    /// no resultado, e um código repetido no lote devolve o cadastro uma só vez. A comparação usa os códigos exatos,
    /// sem aparar nem normalizar.
    /// </summary>
    Task<IReadOnlyList<RecursoAcessibilidadeView>> ListarVivosPorCodigosAsync(
        IReadOnlyCollection<string> codigos,
        CancellationToken cancellationToken = default);
}
