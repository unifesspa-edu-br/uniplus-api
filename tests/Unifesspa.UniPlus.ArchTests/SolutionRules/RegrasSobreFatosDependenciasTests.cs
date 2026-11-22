namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using ArchUnitNET.xUnit;

using AwesomeAssertions;

using Unifesspa.UniPlus.ArchTests.TestSupport;
using Unifesspa.UniPlus.Kernel.Domain.Entities;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

/// <summary>
/// Fronteira do projeto compartilhado de regras sobre fatos do candidato (ADR-0135).
/// </summary>
/// <remarks>
/// Conferida pelas referências de assembly compiladas, e não por dependência entre tipos:
/// é a referência de projeto que cria o acoplamento que a ADR veda. As camadas dos módulos
/// podem depender das regras livremente; o que a regra impede é o projeto crescer para
/// fora do papel de lógica pura sobre valores, ou virar dependência de outro projeto
/// compartilhado de base.
/// </remarks>
public sealed class RegrasSobreFatosDependenciasTests
{
    [Fact(DisplayName = "as regras sobre fatos ficam dentro da fronteira da ADR-0135")]
    public void RegrasSobreFatos_RespeitamAFronteira()
    {
        ConferirReferenciasDasRegras();
        Classes().That().ResideInNamespaceMatching(@"^Unifesspa\.UniPlus\.Regras(\.|$)")
            .Should().NotBeAssignableTo(typeof(EntityBase))
            .Check(ArquiteturaDasTravas.Produto);
        ConferirReferenciasDaBase();
    }

    private static void ConferirReferenciasDasRegras()
    {
        string[] referencias = [.. ArquiteturaDasTravas.Produto.Assemblies
            .Single(static a => a.Name == "Unifesspa.UniPlus.Regras").ReferencedAssemblyNames];

        // Lista do que é permitido, não do que é proibido: qualquer pacote de terceiro que
        // entre no projeto (validação, persistência, log) quebra a fronteira, não só os
        // três que ocorreriam primeiro.
        referencias.Where(n => n != "Unifesspa.UniPlus.Kernel"
                && n is not ("netstandard" or "mscorlib" or "System")
                && !n.StartsWith("System.", StringComparison.Ordinal))
            .Should().BeEmpty("o projeto de regras depende só do Kernel e do .NET");
    }

    private static void ConferirReferenciasDaBase()
    {
        // O Kernel não entra: referenciar as regras de volta seria ciclo, que a build já recusa.
        string[] baseCompartilhada =
            ["Unifesspa.UniPlus.Governance.Contracts", "Unifesspa.UniPlus.Application.Abstractions", "Unifesspa.UniPlus.Authorization"];
        ArchUnitNET.Domain.Assembly[] assemblies = [.. ArquiteturaDasTravas.Produto.Assemblies
            .Where(a => baseCompartilhada.Contains(a.Name, StringComparer.Ordinal))];
        assemblies.Select(static a => a.Name).Should().BeEquivalentTo(baseCompartilhada);
        assemblies
            .Where(static a => a.ReferencedAssemblyNames.Contains("Unifesspa.UniPlus.Regras", StringComparer.Ordinal))
            .Should().BeEmpty("fora das camadas dos módulos, só Infrastructure.Core depende das regras, para registrar os erros");
    }
}
