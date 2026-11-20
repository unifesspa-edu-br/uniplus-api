namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Persistence.Apoio;

using Microsoft.EntityFrameworkCore.Metadata;

/// <summary>Leitura dos metadados do modelo do EF que falha dizendo o que faltou, em vez de devolver nulo.</summary>
internal static class MetadadosDoModeloExtensions
{
    public static IEntityType Entidade(this IModel modelo, Type tipo)
    {
        ArgumentNullException.ThrowIfNull(modelo);
        ArgumentNullException.ThrowIfNull(tipo);

        return modelo.FindEntityType(tipo) ?? throw new InvalidOperationException($"O modelo não tem a entidade {tipo.Name}.");
    }

    public static IProperty Propriedade(this ITypeBase tipo, string nome)
    {
        ArgumentNullException.ThrowIfNull(tipo);

        return tipo.FindProperty(nome) ?? throw new InvalidOperationException($"O tipo {tipo.DisplayName()} não tem a propriedade {nome}.");
    }

    public static IComplexProperty PropriedadeComplexa(this ITypeBase tipo, string nome)
    {
        ArgumentNullException.ThrowIfNull(tipo);

        return tipo.FindComplexProperty(nome) ?? throw new InvalidOperationException($"O tipo {tipo.DisplayName()} não tem a propriedade complexa {nome}.");
    }

    public static IElementType Elemento(this IProperty propriedade)
    {
        ArgumentNullException.ThrowIfNull(propriedade);

        return propriedade.GetElementType() ?? throw new InvalidOperationException($"A propriedade {propriedade.Name} não é uma coleção primitiva.");
    }
}
