namespace Unifesspa.UniPlus.Verificador.ArchTests;

using ArchUnitNET.Domain;
using ArchUnitNET.Loader;

using ReflectionAssembly = System.Reflection.Assembly;

/// <summary>
/// Os nomes que um assembly alcança, direta e transitivamente. O ArchLoader carrega o assembly e as dependências dele
/// que estão no diretório de saída; o manifesto de cada assembly carregado traz os nomes que ele referencia, inclusive
/// os de dependência que não chegou ao diretório e por isso não foi carregada.
/// </summary>
internal static class AssembliesAlcancados
{
    internal static HashSet<string> DoAssembly(ReflectionAssembly raiz)
    {
        Architecture arquitetura = new ArchLoader().LoadAssemblyIncludingDependencies(raiz, recursive: true).Build();

        return arquitetura.Assemblies
            .SelectMany(static assembly => assembly.ReferencedAssemblyNames.Append(assembly.Name))
            .ToHashSet(StringComparer.Ordinal);
    }

    internal static ReflectionAssembly DoDiretorioDeSaida(string nome) =>
        ReflectionAssembly.LoadFrom(Path.Join(AppContext.BaseDirectory, $"{nome}.dll"));
}
