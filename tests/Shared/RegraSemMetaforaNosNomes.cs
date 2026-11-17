namespace Unifesspa.UniPlus.Testes.Compartilhado;

using ArchUnitNET.Domain;
using ArchUnitNET.Fluent.Syntax.Elements.Types;
using ArchUnitNET.Loader;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

using ReflectionAssembly = System.Reflection.Assembly;

/// <summary>
/// A regra que impede "retrato" no nome de tipo e de membro de um assembly: a metáfora não diz o que a coisa guarda.
/// Lê os metadados do assembly, nunca o texto do código; por isso variáveis locais, nomes de exibição e comentários
/// ficam de fora. Os métodos auxiliares de teste e os de teste entram, porque são membros.
/// </summary>
internal static class RegraSemMetaforaNosNomes
{
    private const string Termo = "Retrato";

    /// <summary>As violações do assembly, restritas aos tipos cujo namespace casa com o padrão, quando dado.</summary>
    internal static IReadOnlyList<string> Violacoes(ReflectionAssembly assembly, string padraoDeNamespace = ".*")
    {
        ArgumentNullException.ThrowIfNull(assembly);

        Architecture arquitetura = new ArchLoader().LoadAssemblies(assembly).Build();
        GivenTypesConjunction tipos = Types().That().ResideInAssembly(assembly).And().ResideInNamespaceMatching(padraoDeNamespace);

        return
        [
            .. tipos.Should().NotHaveNameContaining(Termo).Evaluate(arquitetura).Where(static r => !r.Passed).Select(static r => r.Description),
            .. Members().That().AreDeclaredIn(tipos).Should().NotHaveNameContaining(Termo).Evaluate(arquitetura)
                .Where(static r => !r.Passed).Select(static r => r.Description),
        ];
    }

    /// <summary>Quantos tipos do assembly a regra enxerga, para o teste mostrar que ela não passa vazia.</summary>
    internal static int TiposVistos(ReflectionAssembly assembly, string padraoDeNamespace = ".*")
    {
        ArgumentNullException.ThrowIfNull(assembly);

        Architecture arquitetura = new ArchLoader().LoadAssemblies(assembly).Build();

        return Types().That().ResideInAssembly(assembly).And().ResideInNamespaceMatching(padraoDeNamespace).GetObjects(arquitetura).Count();
    }
}
