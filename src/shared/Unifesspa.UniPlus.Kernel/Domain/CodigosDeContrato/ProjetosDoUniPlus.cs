namespace Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

using System.Reflection;

/// <summary>
/// Os projetos do Uni+ que um executável usa, descobertos pelas referências de assembly dele, para que a conferência na
/// subida alcance todo enum com código sem uma lista de nomes escrita à mão.
/// </summary>
public static class ProjetosDoUniPlus
{
    private const string PrefixoDoNome = "Unifesspa.UniPlus.";

    /// <summary>
    /// O executável e todos os assemblies <c>Unifesspa.UniPlus.*</c> que ele alcança, direta e transitivamente, pelas
    /// referências registradas no metadado. Só entra o projeto de que algum código é de fato usado: o compilador não
    /// registra a referência que o código não usa. Cada assembly aparece uma vez, em ordem ordinal do nome.
    /// </summary>
    /// <exception cref="FileNotFoundException">
    /// Uma referência não carrega. A falha sobe de propósito: um projeto que não se alcança é um projeto que não foi
    /// conferido.
    /// </exception>
    public static IReadOnlyList<Assembly> UsadosPor(Assembly executavel)
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
