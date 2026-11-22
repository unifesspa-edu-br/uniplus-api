namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Fluent.Syntax.Elements.Types.Classes;
using ArchUnitNET.xUnit;

using AwesomeAssertions;

using Unifesspa.UniPlus.ArchTests.TestSupport;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

public sealed class ForensicEntityConventionsTests
{
    [Fact]
    public void EntidadesForenses_NaoHerdamEntityBase_ESaoSeladas()
    {
        TodasAsEntidades().GetObjects(ArquiteturaDasTravas.Produto).Should().NotBeEmpty();
        TodasAsEntidades().Should().NotBeAssignableTo(typeof(EntityBase)).AndShould().BeSealed()
            .Check(ArquiteturaDasTravas.Produto);
    }

    [Fact]
    public void Convencoes_RecusamHerancaESelagemAusente()
    {
        GivenClassesConjunction entidades = Classes().That().ResideInNamespace("FixturesForenses")
            .And().ImplementInterface(typeof(IForensicEntity)).And().AreNotAbstract();
        IArchRule regra = entidades.Should().NotBeAssignableTo(typeof(EntityBase)).AndShould().BeSealed();
        ArquiteturaDasTravas.Reprovacoes(regra, ArquiteturaDasTravas.Fixtures).Should().HaveCount(2);
    }

    [Fact]
    public void Convencoes_RecusamConstrutorPublico()
    {
        Class entidade = Classes().That().Are(typeof(FixturesForenses.EntidadeNaoSelada))
            .GetObjects(ArquiteturaDasTravas.Fixtures).Single();
        IArchRule regra = MethodMembers().That().AreConstructors().And().AreDeclaredIn(entidade).Should().NotBePublic();
        ArquiteturaDasTravas.Reprovacoes(regra, ArquiteturaDasTravas.Fixtures).Should().NotBeEmpty();
    }

    [Fact]
    public void Convencoes_IdentificamAusenciaDeFactory()
    {
        Class entidade = Classes().That().Are(typeof(FixturesForenses.EntidadeNaoSelada))
            .GetObjects(ArquiteturaDasTravas.Fixtures).Single();
        MethodMembers().That().AreDeclaredIn(entidade).And().ArePublic().And().AreStatic()
            .And().HaveReturnType(entidade).GetObjects(ArquiteturaDasTravas.Fixtures).Should().BeEmpty();
    }

    [Fact]
    public void EntidadesForenses_ExigemFactoryEstatica()
    {
        Class[] entidades = [.. Entidades().GetObjects(ArquiteturaDasTravas.Produto)];
        entidades.Should().NotBeEmpty();
        MethodMembers().That().AreConstructors().And().AreDeclaredIn(entidades)
            .Should().NotBePublic().Check(ArquiteturaDasTravas.Produto);

        foreach (Class entidade in entidades)
        {
            MethodMembers().That().AreDeclaredIn(entidade).And().ArePublic().And().AreStatic()
                .And().HaveReturnType(entidade).GetObjects(ArquiteturaDasTravas.Produto)
                .Should().NotBeEmpty($"{entidade.FullName} deve ter factory pública");
        }
    }

    private static GivenClassesConjunction TodasAsEntidades() =>
        Classes().That().ResideInNamespaceMatching(@"^Unifesspa\.UniPlus\.")
            .And().ImplementInterface(typeof(IForensicEntity)).And().AreNotAbstract();

    private static GivenClassesConjunction Entidades() =>
        Classes().That().ResideInNamespaceMatching(@"^Unifesspa\.UniPlus\.(Selecao|Publicacoes|Configuracao)\.Domain(\.|$)")
            .And().ImplementInterface(typeof(IForensicEntity)).And().AreNotAbstract();
}
