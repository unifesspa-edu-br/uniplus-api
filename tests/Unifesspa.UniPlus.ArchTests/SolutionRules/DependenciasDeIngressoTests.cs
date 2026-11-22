namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using ArchUnitNET.Fluent.Syntax.Elements.Types;
using ArchUnitNET.xUnit;

using AwesomeAssertions;

using Unifesspa.UniPlus.ArchTests.TestSupport;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

public sealed class DependenciasDeIngressoTests
{
    [Fact]
    public void DominioEAplicacao_NaoDependemDeWolverine() =>
        Regra(@"^Unifesspa\.UniPlus\.Ingresso\.(Domain|Application)(\.|$)").Check(ArquiteturaDasTravas.Produto);

    [Fact]
    public void Regra_RecusaDependenciaDeWolverine() =>
        ArquiteturaDasTravas.Reprovacoes(Regra(@"^Unifesspa\.UniPlus\.ArchTests\.Fixtures\.DependenciasDeIngresso$"), ArquiteturaDasTravas.Fixtures)
            .Should().ContainSingle();

    private static TypesShouldConjunction Regra(string escopo) => Types().That().ResideInNamespaceMatching(escopo)
        .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(@"^Wolverine(\.|$)");
}
