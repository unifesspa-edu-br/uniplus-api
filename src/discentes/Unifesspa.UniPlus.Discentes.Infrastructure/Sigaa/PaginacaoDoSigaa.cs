namespace Unifesspa.UniPlus.Discentes.Infrastructure.Sigaa;

/// <summary>
/// Como a varredura dos vínculos percorre as páginas da origem.
/// </summary>
public sealed class PaginacaoDoSigaa
{
    /// <summary>
    /// Limite que a origem impõe ao tamanho de página. Pedir mais que isso é recusado por ela.
    /// </summary>
    public const int MaximoItensPorPagina = 200;

    /// <summary>
    /// Quantos vínculos pedir por página, limitado a <see cref="MaximoItensPorPagina"/>.
    /// </summary>
    public int ItensPorPagina { get; init; } = MaximoItensPorPagina;

    /// <summary>
    /// Quantas páginas buscar ao mesmo tempo. Um significa varredura sequencial.
    /// </summary>
    public int GrauDeParalelismo { get; init; } = 5;
}
