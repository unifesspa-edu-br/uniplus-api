namespace Unifesspa.UniPlus.Authorization.Contracts;

using System.Collections.Frozen;

/// <summary>
/// Cópias defensivas <b>imutáveis</b> para as coleções dos tipos de contrato.
/// Os tipos da assinatura de decisão (ADR-0078) são <i>records</i> imutáveis;
/// expor uma coleção recebida diretamente permitiria mutação pós-construção
/// pela referência de origem. Estes ajudantes materializam uma cópia imutável,
/// tratando <c>null</c> como coleção vazia.
/// </summary>
internal static class ColecoesSomenteLeitura
{
    /// <summary>Cópia somente-leitura e imutável de uma sequência, preservando a ordem.</summary>
    public static IReadOnlyList<T> Lista<T>(IEnumerable<T>? itens)
        => Array.AsReadOnly((itens ?? []).ToArray());

    /// <summary>Cópia imutável de um conjunto, sem duplicatas.</summary>
    public static IReadOnlySet<T> Conjunto<T>(IEnumerable<T>? itens)
        => (itens ?? []).ToFrozenSet();
}
