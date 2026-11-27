namespace Unifesspa.UniPlus.Infrastructure.Core.Pagination;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Leitura do código de contrato de uma enumeração que viajou no ponto de continuação do cursor. O código que o
/// contrato não declara é defeito do cursor, nunca um valor padrão.
/// </summary>
public static class CodigoDoCursor
{
    /// <exception cref="CursorAnchorMismatchException">O texto não é um código declarado de <typeparamref name="TEnum"/>.</exception>
    public static TEnum Ler<TEnum>(string texto)
        where TEnum : struct, Enum =>
        CatalogoDe<TEnum>.TentarObter(texto, out TEnum valor)
            ? valor
            : throw new CursorAnchorMismatchException();
}
