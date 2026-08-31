namespace Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Leitor cross-módulo de <c>TipoDocumento</c> (ADR-0056). Expõe o estado vivo do
/// cadastro classificatório de tipos de documento para consumo por outros bounded
/// contexts (ex.: a configuração de exigências documentais do Módulo Seleção, que
/// referencia o tipo ao montar a relação de exigências de um edital) sem acesso
/// direto ao banco de Configuração (ADR-0054).
/// </summary>
public interface ITipoDocumentoReader
{
    /// <summary>
    /// Lista todos os tipos de documento vivos (não soft-deleted), ordenados por
    /// <c>Codigo</c> ascendente para determinismo cross-cliente.
    /// </summary>
    Task<IReadOnlyList<TipoDocumentoView>> ListarVivosAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém um tipo de documento pelo <paramref name="id"/>, ou
    /// <see langword="null"/> se inexistente / soft-deleted.
    /// </summary>
    Task<TipoDocumentoView?> ObterPorIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém um tipo de documento vivo pela chave natural — o
    /// <paramref name="codigo"/> —, ou <see langword="null"/> se inexistente /
    /// soft-deleted. O valor de busca é aparado antes da comparação, que é ordinal.
    /// </summary>
    /// <remarks>
    /// <para>Existe para quem referencia o tipo por código e precisa recusar a referência
    /// na escrita — sem isso a única alternativa é listar tudo e filtrar em memória.
    /// Espelha <see cref="ITipoEtapaReader.ObterAtivoPorCodigoAsync"/>.</para>
    /// <para>O código tem formato fechado — letra maiúscula inicial, depois maiúsculas,
    /// dígitos e sublinhado —, o que dispensa normalização Unicode: não há caractere
    /// componível no conjunto aceito. Um valor de busca fora desse formato não pode
    /// corresponder a registro algum e devolve <see langword="null"/> sem consultar o
    /// banco.</para>
    /// </remarks>
    Task<TipoDocumentoView?> ObterVivoPorCodigoAsync(
        string codigo,
        CancellationToken cancellationToken = default);
}
