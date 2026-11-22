namespace Unifesspa.UniPlus.Contratos.UnitTests.SolutionRules;

using System.Reflection;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Catalogo;
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
        Assembly[] assemblies = [.. Directory.GetFiles(AppContext.BaseDirectory, "Unifesspa.UniPlus.*.API.dll")
            .Select(static caminho => Assembly.LoadFrom(caminho))];
        assemblies.Should().NotBeEmpty();
        (int Usos, string? Falha)[] resultados = [.. assemblies.SelectMany(TiposCarregaveis)
            .SelectMany(static tipo => tipo.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Append(tipo)).Select(ConferirMembro)];
        string[] falhas = [.. resultados.Select(static resultado => resultado.Falha).OfType<string>()];
        int usos = resultados.Sum(static resultado => resultado.Usos);

        falhas.Should().BeEmpty();
        usos.Should().BeGreaterThan(0, "a varredura precisa alcançar os usos para valer como conferência");
    }

    [Fact(DisplayName = "Valor que não é membro do enum falha ao construir o atributo, e não em requisição")]
    public void ExigePermissao_ValorIndefinido_FalhaNaConstrucao()
    {
        Func<ExigePermissaoAttribute<PermissaoAutorizacao>> criar = () => new((PermissaoAutorizacao)999);

        criar.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*999*PermissaoAutorizacao*");
    }

    private static (int Usos, string? Falha) ConferirMembro(MemberInfo membro)
    {
        try
        {
            int usos = membro.GetCustomAttributes(inherit: false).Count(static atributo => atributo.GetType().IsGenericType
                && atributo.GetType().GetGenericTypeDefinition() == typeof(ExigePermissaoAttribute<>));
            return (usos, null);
        }
        catch (TargetInvocationException erro) when (erro.InnerException is not null)
        {
            return (0, DescreverFalha(membro, erro.InnerException));
        }
        catch (ArgumentException erro)
        {
            return (0, DescreverFalha(membro, erro));
        }
    }

    private static string DescreverFalha(MemberInfo membro, Exception erro) =>
        $"{membro.DeclaringType?.Name}.{membro.Name}: {erro.GetType().Name} — {erro.Message}";

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
