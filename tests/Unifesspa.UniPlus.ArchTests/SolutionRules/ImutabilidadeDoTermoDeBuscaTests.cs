namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using ArchUnitNET.Domain;
using ArchUnitNET.xUnit;

using AwesomeAssertions;

using Unifesspa.UniPlus.ArchTests.TestSupport;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

public sealed class ImutabilidadeDoTermoDeBuscaTests
{
    [Fact]
    public void TermoDeBusca_Construtores_NaoSaoPublicos() =>
        MethodMembers().That().AreConstructors().And().AreDeclaredIn(typeof(TermoDeBusca))
            .Should().NotBePublic().Check(ArquiteturaDasTravas.Produto);

    [Fact]
    public void TermoDeBusca_Propriedades_NaoTemSetter() =>
        SomenteLeitura(typeof(TermoDeBusca), ArquiteturaDasTravas.Produto).Should().BeTrue();

    [Fact]
    public void Regra_PropriedadesComSetOuInit_SaoRecusadas()
    {
        SomenteLeitura(typeof(ComInit), ArquiteturaDasTravas.Fixtures).Should().BeFalse();
        SomenteLeitura(typeof(ComSet), ArquiteturaDasTravas.Fixtures).Should().BeFalse();
        SomenteLeitura(typeof(SemSet), ArquiteturaDasTravas.Fixtures).Should().BeTrue();
    }

    private static bool SomenteLeitura(Type tipo, Architecture arquitetura) =>
        PropertyMembers().That().AreDeclaredIn(tipo).GetObjects(arquitetura)
            .All(p => PropertyMembers().That().AreDeclaredIn(tipo).And().HaveNoSetter().GetObjects(arquitetura).Contains(p));

    private sealed class ComInit
    {
        public string Valor { get; init; } = string.Empty;
    }

    private sealed class ComSet
    {
        public string Valor { get; set; } = string.Empty;
    }

    private sealed class SemSet
    {
        public string Valor { get; } = string.Empty;
    }
}
