namespace Unifesspa.UniPlus.Infrastructure.Core.Pagination;

/// <summary>
/// Página cursor-based (ADR-0026). O wire format de coleção é o
/// <see cref="Items"/> serializado como array JSON puro (ADR-0025); o
/// <see cref="Links"/> é entregue ao cliente via header <c>Link</c>
/// (RFC 5988/8288), não no body.
/// </summary>
public sealed record Page<T>(IReadOnlyList<T> Items, PageLinks Links);
