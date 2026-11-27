namespace Unifesspa.UniPlus.Infrastructure.Core.Pagination;

/// <summary>
/// Ponto de continuação serializado no cursor: <see cref="After"/> é o <c>Id</c> do item de fronteira e
/// <see cref="SortKey"/> é a chave de continuação (ADR-0094), presente em toda listagem.
/// </summary>
internal readonly record struct PontoDeContinuacao(string After, string SortKey)
{
    /// <summary>Ponto de continuação a partir da chave e do <c>Id</c> de desempate; <c>null</c> quando não há página para esse lado.</summary>
    public static PontoDeContinuacao? De((string SortKey, Guid Id)? ponto) =>
        ponto is { } valor ? new PontoDeContinuacao(valor.Id.ToString(), valor.SortKey) : null;
}
