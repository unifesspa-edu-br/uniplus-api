namespace Unifesspa.UniPlus.ArchTests.TestSupport;

using System.Text.RegularExpressions;

using ArchUnitNET.Domain;

/// <summary>
/// As classes que implementam uma interface, a declarada na própria classe ou a herdada de uma classe-base. A regra que
/// se aplica a quem implementa a interface precisa alcançar os dois, ou uma classe derivada a contornaria.
/// </summary>
internal static class ImplementadoresDeInterface
{
    internal static List<Class> Em(Architecture arquitetura, Func<Interface, bool> interfaceDeInteresse) =>
        [.. arquitetura.Classes.Where(classe => Implementa(classe, interfaceDeInteresse))];

    /// <summary>
    /// O padrão de nome completo que alcança as classes dadas e os tipos aninhados delas: a máquina de estados de cada método
    /// assíncrono e a classe de cada lambda contam como a classe que os contém.
    /// </summary>
    internal static string PadraoComAninhados(IEnumerable<Class> classes) =>
        "^(" + string.Join('|', classes.Select(static c => Regex.Escape(c.FullName))) + @")(\+|$)";

    private static bool Implementa(Class classe, Func<Interface, bool> interfaceDeInteresse) =>
        classe.ImplementedInterfaces.OfType<Interface>().Any(interfaceDeInteresse)
        || (classe.BaseClass is { } baseDaClasse && Implementa(baseDaClasse, interfaceDeInteresse));
}
