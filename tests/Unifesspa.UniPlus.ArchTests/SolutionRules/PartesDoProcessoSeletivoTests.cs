namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using ArchUnitNET.Domain;
using ArchUnitNET.xUnit;

using Unifesspa.UniPlus.ArchTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

public sealed class PartesDoProcessoSeletivoTests
{
    [Fact(DisplayName = "A6: toda parte do processo está em Domain.Entities")]
    public void A6_PartesDoProcessoVivemNasEntidades()
    {
        Interface parte = Interfaces().That().HaveFullName("Unifesspa.UniPlus.Selecao.Domain.Entities.IParteDoProcesso")
            .GetObjects(ArquiteturaDasTravas.Produto).Single();
        Classes().That().HaveFullNameStartingWith("Unifesspa.UniPlus.").And().ImplementInterface(parte)
            .Should().ResideInNamespace(typeof(ProcessoSeletivo).Namespace!)
            .Check(ArquiteturaDasTravas.Produto);
    }
}
