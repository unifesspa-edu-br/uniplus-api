namespace Unifesspa.UniPlus.Infrastructure.Core.Pagination;

/// <summary>
/// Ponto de continuação serializado no cursor: <see cref="After"/> é o <c>Id</c> do item de fronteira
/// (sempre) e <see cref="SortKey"/> só existe no keyset ordenado (ADR-0094).
/// </summary>
internal readonly record struct PontoDeContinuacao(string After, string? SortKey)
{
    /// <summary>Ponto de continuação do keyset por <c>Id</c>; <c>null</c> quando não há página para esse lado.</summary>
    public static PontoDeContinuacao? PorId(Guid? id) =>
        id is { } valor ? new PontoDeContinuacao(valor.ToString(), SortKey: null) : null;

    /// <summary>Ponto de continuação do keyset ordenado: chave de ordenação e <c>Id</c> de desempate.</summary>
    public static PontoDeContinuacao? Ordenada((string SortKey, Guid Id)? ponto) =>
        ponto is { } valor ? new PontoDeContinuacao(valor.Id.ToString(), valor.SortKey) : null;
}
