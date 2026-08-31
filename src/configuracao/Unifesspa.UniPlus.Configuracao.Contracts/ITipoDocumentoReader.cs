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
    /// <para><c>Trim</c> e nada mais, de propósito: é a normalização que o cadastro aplica
    /// ao gravar. Diferente do tipo de etapa e da modalidade, o código do tipo de documento
    /// não passa por NFC nem por formato fechado na escrita — buscar em forma composta o
    /// que o banco guarda decomposto daria por inexistente um tipo que existe.</para>
    /// </remarks>
    Task<TipoDocumentoView?> ObterVivoPorCodigoAsync(
        string codigo,
        CancellationToken cancellationToken = default);
}
