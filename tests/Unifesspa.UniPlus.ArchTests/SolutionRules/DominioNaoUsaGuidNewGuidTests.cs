namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;

using AwesomeAssertions;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

using ReflectionAssembly = System.Reflection.Assembly;

/// <summary>O domínio gera identificadores UUID v7, conforme a ADR-0032.</summary>
public sealed class DominioNaoUsaGuidNewGuidTests
{
    private static readonly Architecture Arquitetura = CarregarArquitetura();

    [Fact(DisplayName = "O domínio não chama Guid.NewGuid")]
    public void Dominio_NaoChama_GuidNewGuid()
    {
        const string dominio = @"^Unifesspa\.UniPlus\.(Kernel(\.|$)|.*\.Domain(\.|$))";
        ArchUnitNET.Fluent.Syntax.Elements.Members.MethodMembers.GivenMethodMembersConjunction chamada = MethodMembers().That()
            .AreDeclaredIn(typeof(Guid))
            .And().HaveNameStartingWith("NewGuid(");

        Types().That().HaveFullName(typeof(CanarioGuid).FullName!).And().CallAny(chamada)
            .GetObjects(Arquitetura).Should().NotBeEmpty("a regra deve detectar uma chamada real");

        Types().That().ResideInNamespaceMatching(dominio).GetObjects(Arquitetura)
            .Should().NotBeEmpty("os assemblies de domínio precisam estar carregados");

        Types().That().ResideInNamespaceMatching(dominio)
            .Should().NotCallAny(chamada)
            .Because("as entidades de domínio usam Guid.CreateVersion7()")
            .Check(Arquitetura);
    }

    private static Architecture CarregarArquitetura()
    {
        ReflectionAssembly[] assemblies =
        [
            typeof(Guid).Assembly,
            typeof(CanarioGuid).Assembly,
            .. Directory.GetFiles(AppContext.BaseDirectory, "Unifesspa.UniPlus.*.Domain.dll")
                .Select(ReflectionAssembly.LoadFrom),
            ReflectionAssembly.Load("Unifesspa.UniPlus.Kernel"),
        ];

        return new ArchLoader().LoadAssemblies(assemblies).Build();
    }

    private static class CanarioGuid
    {
        public static Guid Criar() => Guid.NewGuid();
    }
}
