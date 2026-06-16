namespace Unifesspa.UniPlus.Authorization.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Enums;

public sealed class MotivoNegativaTests
{
    [Fact]
    public void MotivoNegativa_EhConjuntoFechadoDe13Valores()
    {
        Enum.GetValues<MotivoNegativa>().Should().HaveCount(13,
            "ADR-0078 define um conjunto fechado de 13 motivos de negativa");
    }

    [Fact]
    public void MotivoNegativa_ValoresSaoDistintos()
    {
        int[] valores = Enum.GetValues<MotivoNegativa>().Cast<int>().ToArray();

        valores.Should().OnlyHaveUniqueItems();
    }
}
