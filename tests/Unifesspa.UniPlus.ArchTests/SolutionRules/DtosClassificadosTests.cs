namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using ArchUnitNET.xUnit;

using Unifesspa.UniPlus.ArchTests.TestSupport;
using Unifesspa.UniPlus.Authorization.Projecao;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

public sealed class DtosClassificadosTests
{
    [Fact]
    public void ItensClassificados_FicamNoNamespaceAdministrativo()
    {
        Classes().That().ResideInNamespaceMatching(@"^Unifesspa\.UniPlus\.")
            .And().AreAssignableTo(typeof(ItemClassificado)).And().AreNot(typeof(ItemClassificado))
            .Should().ResideInNamespaceMatching(@"\.DTOs\.Administrativos$")
            .Check(ArquiteturaDasTravas.Produto);

        PropertyMembers().That().AreDeclaredIn(Types().That().ResideInNamespaceMatching(@"^Unifesspa\.UniPlus\."))
            .And().HaveAnyAttributes(typeof(ProtegidoComoAttribute))
            .Should().BeDeclaredIn(Types().That().ResideInNamespaceMatching(@"\.DTOs\.Administrativos$"))
            .Check(ArquiteturaDasTravas.Produto);
    }

    [Fact]
    public void ItensClassificados_HerdamProtecaoDaBase() =>
        Classes().That().ResideInNamespaceMatching(@"^Unifesspa\.UniPlus\.")
            .And().ImplementInterface(typeof(IItemClassificado)).And().AreNot(typeof(ItemClassificado))
            .Should().BeAssignableTo(typeof(ItemClassificado)).Check(ArquiteturaDasTravas.Produto);
}
