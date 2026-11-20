namespace Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

using System.Reflection;

/// <summary>
/// Descobre, pelas referências estáticas do metadado, os assemblies do Uni+ que um executável alcança, para que a
/// conferência na subida chegue a todo enum com código sem uma lista de nomes escrita à mão.
/// </summary>
/// <remarks>
/// O alcance é o do metadado, e não o do que a execução carrega. Escapam dele o assembly carregado por nome em texto
/// (<c>Assembly.Load</c>), o intermediário cujo nome não começa por <c>Unifesspa.UniPlus.</c>, que interrompe a cadeia,
/// e a dependência que o compilador não registrou no metadado porque nenhum código a referencia. A procura carrega cada
/// assembly que alcança, e a falha de carga sobe sem tratamento.
/// </remarks>
public static class AssembliesDoUniPlus
{
    private const string PrefixoDoNome = "Unifesspa.UniPlus.";

    /// <summary>
    /// O executável, que entra mesmo sem o prefixo <c>Unifesspa.UniPlus.</c> no nome, e os assemblies que ele alcança por
    /// uma cadeia contínua de referências estáticas cujos nomes têm esse prefixo, direta ou transitivamente. Cada
    /// assembly aparece uma vez, em ordem ordinal do nome.
    /// </summary>
    /// <exception cref="ArgumentNullException">O executável é nulo.</exception>
    /// <exception cref="FileNotFoundException">
    /// Uma referência registrada no metadado não carrega. A falha sobe de propósito: um assembly alcançado e não
    /// carregado é um assembly que não foi conferido.
    /// </exception>
    public static IReadOnlyList<Assembly> AlcancadosAPartirDe(Assembly executavel)
    {
        ArgumentNullException.ThrowIfNull(executavel);

        HashSet<Assembly> alcancados = [executavel];
        Percorrer(executavel, alcancados);

        return [.. alcancados.OrderBy(static a => a.GetName().Name, StringComparer.Ordinal)];
    }

    private static void Percorrer(Assembly assembly, HashSet<Assembly> alcancados)
    {
        foreach (Assembly referenciado in ReferenciadosDoUniPlus(assembly))
        {
            if (alcancados.Add(referenciado))
            {
                Percorrer(referenciado, alcancados);
            }
        }
    }

    private static IEnumerable<Assembly> ReferenciadosDoUniPlus(Assembly assembly) =>
        assembly.GetReferencedAssemblies()
            .Where(static nome => nome.Name?.StartsWith(PrefixoDoNome, StringComparison.Ordinal) == true)
            .Select(static nome => Assembly.Load(nome));
}
