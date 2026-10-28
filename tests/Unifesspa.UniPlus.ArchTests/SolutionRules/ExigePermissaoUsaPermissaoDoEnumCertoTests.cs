namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using System.Reflection;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Authorization;

/// <summary>
/// O construtor de <c>[ExigePermissao&lt;T&gt;]</c> recebe o valor como <c>object</c>, porque o
/// C# não admite parâmetro de atributo do tipo genérico (CS0181). Construir o atributo é o
/// que confere que o valor é do enum <c>T</c> e existe no catálogo; este teste constrói todos
/// os usos do produto, de modo que um engano falha aqui e não na primeira requisição (ADR-0151).
/// </summary>
public sealed class ExigePermissaoUsaPermissaoDoEnumCertoTests
{
    [Fact(DisplayName = "Todo [ExigePermissao<T>] do produto constrói com uma permissão do enum T")]
    public void ExigePermissao_TodosOsUsosConstroem()
    {
        // O CLR só carrega o assembly quando um tipo dele é usado; carregar da pasta de saída
        // torna a varredura independente da ordem em que os testes rodam.
        Assembly[] assemblies = [.. Directory
            .GetFiles(AppContext.BaseDirectory, "Unifesspa.UniPlus.*.API.dll")
            .Select(static caminho => Assembly.LoadFrom(caminho))];

        assemblies.Should().NotBeEmpty();

        List<string> falhas = [];
        int usos = 0;
        foreach (MemberInfo membro in assemblies.SelectMany(static a => TiposCarregaveis(a))
            .SelectMany(static t => t.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Append(t)))
        {
            try
            {
                usos += membro.GetCustomAttributes(inherit: false)
                    .Count(static a => a.GetType().IsGenericType
                        && a.GetType().GetGenericTypeDefinition() == typeof(ExigePermissaoAttribute<>));
            }
            catch (Exception ex) when (ex is TargetInvocationException or ArgumentException)
            {
                falhas.Add($"{membro.DeclaringType?.Name}.{membro.Name}: {(ex.InnerException ?? ex).Message}");
            }
        }

        falhas.Should().BeEmpty();
        usos.Should().BeGreaterThan(0, "a varredura precisa alcançar os usos para valer como conferência");
    }

    private static IEnumerable<Type> TiposCarregaveis(Assembly assembly)
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
