namespace Unifesspa.UniPlus.ArchTests.TestSupport;

using Microsoft.EntityFrameworkCore.Metadata;

/// <summary>
/// Percorre o modelo finalizado e entrega cada ponto que guarda um valor: as propriedades escalares dos tipos de
/// entidade, os próprios (<c>owned</c>) inclusive, as dos tipos complexos, em qualquer nível de aninhamento, e o elemento de
/// cada coleção primitiva. A propriedade que é coleção primitiva entrega o elemento, e não ela mesma; a coleção sem
/// tipo de elemento entrega a propriedade.
/// </summary>
internal static class PontosDoModelo
{
    internal static IReadOnlyList<PontoDoModelo> Em(IModel modelo)
    {
        ArgumentNullException.ThrowIfNull(modelo);

        return [.. modelo.GetEntityTypes().SelectMany(tipo => Em(tipo, tipo.DisplayName()))];
    }

    /// <summary>Os pontos que guardam enum com código de contrato e não o gravam pelo conversor do código.</summary>
    internal static IReadOnlyList<PontoDoModelo> QueNaoGravamPeloCodigo(IModel modelo) =>
        [.. Em(modelo).Where(ponto => ponto.NaoGravaPeloCodigo)];

    private static IEnumerable<PontoDoModelo> Em(ITypeBase tipo, string caminho) =>
        tipo.GetDeclaredProperties().Select(propriedade => PontoDe(propriedade, caminho))
            .Concat(tipo.GetDeclaredComplexProperties().SelectMany(complexa => Em(complexa.ComplexType, $"{caminho}.{complexa.Name}")));

    private static PontoDoModelo PontoDe(IProperty propriedade, string caminho) =>
        propriedade.GetElementType() is { } elemento
            ? new PontoDoModelo($"O elemento de {caminho}.{propriedade.Name}", propriedade.Name, elemento.ClrType, elemento.FindTypeMapping()?.Converter)
            : new PontoDoModelo($"{caminho}.{propriedade.Name}", propriedade.Name, propriedade.ClrType, propriedade.FindTypeMapping()?.Converter);
}
