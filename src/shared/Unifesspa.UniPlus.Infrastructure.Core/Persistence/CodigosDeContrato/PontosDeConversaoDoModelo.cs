namespace Unifesspa.UniPlus.Infrastructure.Core.Persistence.CodigosDeContrato;

using Microsoft.EntityFrameworkCore.Metadata;

/// <summary>
/// Percorre o modelo e entrega cada propriedade escalar e cada elemento de coleção primitiva: dos tipos de entidade,
/// os próprios (<c>owned</c>) inclusive, e dos tipos complexos, em qualquer nível de aninhamento. A propriedade que é uma
/// coleção primitiva entrega o elemento, e não ela mesma.
/// </summary>
internal static class PontosDeConversaoDoModelo
{
    public static IEnumerable<IPontoDeConversao> Em(IMutableModel modelo) =>
        modelo.GetEntityTypes().SelectMany(static tipo => Em(tipo));

    private static IEnumerable<IPontoDeConversao> Em(IMutableTypeBase tipo) =>
        tipo.GetDeclaredProperties().Select(PontoDe)
            .Concat(tipo.GetDeclaredComplexProperties().SelectMany(static complexa => Em(complexa.ComplexType)));

    private static IPontoDeConversao PontoDe(IMutableProperty propriedade) =>
        propriedade.GetElementType() is { } elemento
            ? new PontoDoElemento(propriedade, elemento)
            : new PontoDaPropriedade(propriedade);
}
