namespace Unifesspa.UniPlus.Selecao.ArchTests;

using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;

using Unifesspa.UniPlus.Selecao.Domain.Services;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

public sealed class AvaliadorConformidadeLegalEstruturaTests
{
    [Fact]
    public void Avaliador_DeveSerClasseEstatica() =>
        Classes().That().Are(typeof(AvaliadorConformidadeLegal)).Should().BeAbstract().AndShould().BeSealed()
            .Check(new ArchLoader().LoadAssemblies(typeof(AvaliadorConformidadeLegal).Assembly).Build());
}
