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

    /// <summary>
    /// Obtém uma modalidade viva pela chave natural — o <paramref name="codigo"/> —,
    /// ou <see langword="null"/> se inexistente / soft-deleted. A comparação usa o código exato, sem aparar nem normalizar.
    /// </summary>
    /// <remarks>
    /// Existe para quem referencia a modalidade por código e precisa recusar a
    /// referência na escrita: o código congelado no edital é comparado por igualdade
    /// ordinal, então uma referência que nunca vai bater tem de morrer no cadastro.
    /// </remarks>
    Task<ModalidadeView?> ObterVivaPorCodigoAsync(
        string codigo,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// As modalidades vivas (não soft-deleted) dos <paramref name="codigos"/>, numa só leitura, ordenadas por
    /// <c>Codigo</c> ascendente. O código sem cadastro vivo — fora do formato, inexistente ou removido — não aparece
    /// no resultado, e um código repetido no lote devolve o cadastro uma só vez. A comparação usa os códigos exatos,
    /// sem aparar nem normalizar.
    /// </summary>
    Task<IReadOnlyList<ModalidadeView>> ListarVivosPorCodigosAsync(
        IReadOnlyCollection<string> codigos,
        CancellationToken cancellationToken = default);
}
