namespace Unifesspa.UniPlus.Authorization.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Enums;

public sealed class MotivoNegativaTests
{
    [Fact]
    public void MotivoNegativa_EhConjuntoFechadoDe14Valores()
    {
        Enum.GetValues<MotivoNegativa>().Should().HaveCount(14,
            "o conjunto de motivos de negativa é fechado: um motivo novo é decisão, e não detalhe");
    }

    [Fact]
    public void MotivoNegativa_ValoresSaoDistintos()
    {
        int[] valores = Enum.GetValues<MotivoNegativa>().Cast<int>().ToArray();

        valores.Should().OnlyHaveUniqueItems();
    }
}
