namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.xUnit;

using AwesomeAssertions;

using Unifesspa.UniPlus.ArchTests.TestSupport;

using static ArchUnitNET.Fluent.ArchRuleDefinition;


/// <summary>O domínio gera identificadores UUID v7, conforme a ADR-0032.</summary>
public sealed class DominioNaoUsaGuidNewGuidTests
{
    private static readonly Architecture Arquitetura = ArquiteturaDasTravas.Produto;

    [Fact(DisplayName = "O domínio não chama Guid.NewGuid")]
    public void Dominio_NaoChama_GuidNewGuid()
    {
        const string dominio = @"^Unifesspa\.UniPlus\.(Kernel(\.|$)|.*\.Domain(\.|$))";
        ArchUnitNET.Fluent.Syntax.Elements.Members.MethodMembers.GivenMethodMembersConjunction chamada = MethodMembers().That()
            .AreDeclaredIn(typeof(Guid))
            .And().HaveNameStartingWith("NewGuid(");

        Types().That().HaveFullName(typeof(ChamadaDeGuidParaTeste).FullName!).Should().CallAny(chamada)
            .Evaluate(Arquitetura).Should().Contain(static resultado => resultado.Passed, "a regra deve detectar uma chamada real");
        Types().That().ResideInNamespaceMatching(dominio).GetObjects(Arquitetura)
            .Should().NotBeEmpty("os assemblies de domínio precisam estar carregados");

        Types().That().ResideInNamespaceMatching(dominio)
            .Should().NotCallAny(chamada)
            .Because("as entidades de domínio usam Guid.CreateVersion7()")
            .Check(Arquitetura);
    }

    private static class ChamadaDeGuidParaTeste
    {
        public static Guid Criar() => Guid.NewGuid();
    }
}
