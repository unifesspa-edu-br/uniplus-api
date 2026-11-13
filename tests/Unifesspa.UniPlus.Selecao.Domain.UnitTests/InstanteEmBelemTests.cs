namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class InstanteEmBelemTests
{
    [Theory(DisplayName = "O instante usa o offset vigente em Belém na data")]
    [InlineData(2026, 1, 1, -3)]
    [InlineData(1985, 11, 3, -2)]
    public void ResolveOffsetPelaZona(int ano, int mes, int dia, int offsetEsperado)
    {
        TimeSpan esperado = TimeSpan.FromHours(offsetEsperado);
        DateTime local = new(ano, mes, dia, 12, 0, 0, DateTimeKind.Unspecified);

        InstanteEmBelem.OffsetEm(local).Should().Be(esperado);
        InstanteEmBelem.Em(ano, mes, dia, 12, 0, 0).Offset.Should().Be(esperado);
    }
}
