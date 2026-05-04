namespace Unifesspa.UniPlus.Infrastructure.Core.Pagination;

/// <summary>
/// Códigos de domain error que o binder de cursor produz no <c>HttpContext.Items</c>
/// quando o parsing falha. O <see cref="CursorPaginationProblemFactory"/> traduz
/// esses códigos para HTTP status via <c>IDomainErrorMapper</c>, mantendo um
/// único ponto de mapeamento código → status (ADR-0024).
/// </summary>
public static class CursorBindingErrorCodes
{
    public const string Invalido = "Cursor.Invalido";
    public const string Expirado = "Cursor.Expirado";
    public const string LimitInvalido = "Cursor.LimitInvalido";

    /// <summary>Chave em <c>HttpContext.Items</c> onde o binder publica o código do erro.</summary>
    public const string HttpContextItemKey = "__UniPlusCursorBindingError";
}
