namespace Unifesspa.UniPlus.ArchTests.TestSupport;

using System.Reflection;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

/// <summary>
/// Enumera os controllers e as actions dos módulos, a partir dos assemblies <c>*.API</c> da
/// pasta de saída. O CLR só carrega um assembly quando um tipo dele é usado; carregar da pasta
/// torna a varredura independente da ordem em que os testes rodam.
/// </summary>
internal static class ControllersDaSolucao
{
    private const BindingFlags Acoes = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    /// <summary>
    /// Políticas registradas em <c>OidcAuthenticationConfiguration</c> que dizem quem é a pessoa, e não o que ela pode:
    /// <c>Instalacao</c> protege a instalação inicial, que acontece antes de existir concessão para decidir.
    /// </summary>
    internal static readonly string[] PoliticasDeIdentidade = ["Instalacao"];

    internal static IReadOnlyList<Type> De(string modulo)
        => [.. Todos().Where(t => t.Assembly.GetName().Name == $"Unifesspa.UniPlus.{modulo}.API")];

    internal static IReadOnlyList<Type> Todos()
        => [.. Directory
            .GetFiles(AppContext.BaseDirectory, "Unifesspa.UniPlus.*.API.dll")
            .Select(static caminho => Assembly.LoadFrom(caminho))
            .SelectMany(static a => TiposCarregaveis(a))
            .Where(static t => t is { IsClass: true, IsAbstract: false } && typeof(ControllerBase).IsAssignableFrom(t))];

    internal static IReadOnlyList<MethodInfo> AcoesDe(Type controller)
        => [.. controller.GetMethods(Acoes).Where(static m => !m.IsSpecialName
            && m.GetCustomAttributes<HttpMethodAttribute>(inherit: true).Any())];

    /// <summary>Atributos declarados na classe ou na action, que é como o ASP.NET Core os soma.</summary>
    internal static IEnumerable<TAtributo> AtributosDe<TAtributo>(Type controller, MethodInfo? acao = null)
        where TAtributo : Attribute
        => (acao?.GetCustomAttributes<TAtributo>(inherit: true) ?? [])
            .Concat(controller.GetCustomAttributes<TAtributo>(inherit: true));

    internal static IEnumerable<Type> TiposCarregaveis(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.OfType<Type>();
        }
    }
}
