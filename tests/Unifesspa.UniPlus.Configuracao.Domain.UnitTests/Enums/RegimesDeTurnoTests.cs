namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// O parsing do regime de turno é por allowlist textual explícita (UNI-REQ-0137):
/// só REGULAR e INTEGRAL são aceitos. A obrigatoriedade do regime é regra da
/// entidade, não deste mapeamento.
/// </summary>
public sealed class RegimesDeTurnoTests
{
    [Theory(DisplayName = "Os dois tokens canônicos são analisados para o regime correto")]
    [InlineData("REGULAR", RegimeDeTurno.Regular)]
    [InlineData("INTEGRAL", RegimeDeTurno.Integral)]
    public void TryAnalisar_TokenCanonico_Resolve(string token, RegimeDeTurno esperado)
    {
        RegimesDeTurno.TryAnalisar(token, out RegimeDeTurno regime).Should().BeTrue();
        regime.Should().Be(esperado);
    }

    [Fact(DisplayName = "Token com espaços é normalizado por Trim antes da resolução")]
    public void TryAnalisar_ComEspacos_Normaliza()
    {
        RegimesDeTurno.TryAnalisar("  INTEGRAL  ", out RegimeDeTurno regime).Should().BeTrue();
        regime.Should().Be(RegimeDeTurno.Integral);
    }

    [Theory(DisplayName = "Tokens numéricos, PascalCase, fora do domínio e vazios são rejeitados")]
    [InlineData("1")]
    [InlineData("Regular")]
    [InlineData("MATUTINO")]    // turno não é regime
    [InlineData("PARCIAL")]
    [InlineData("regular")]
    [InlineData("")]
    [InlineData("   ")]
    public void TryAnalisar_ForaDoDominio_Rejeita(string token)
    {
        RegimesDeTurno.TryAnalisar(token, out RegimeDeTurno regime).Should().BeFalse();
        regime.Should().Be(RegimeDeTurno.Nenhum);
        RegimesDeTurno.EhValido(token).Should().BeFalse();
    }

    [Fact(DisplayName = "Token nulo é rejeitado sem lançar")]
    public void TryAnalisar_Nulo_Rejeita()
    {
        RegimesDeTurno.TryAnalisar(null, out RegimeDeTurno regime).Should().BeFalse();
        regime.Should().Be(RegimeDeTurno.Nenhum);
    }

    [Theory(DisplayName = "ParaTokenCanonico é o inverso de TryAnalisar (round-trip)")]
    [InlineData(RegimeDeTurno.Regular, "REGULAR")]
    [InlineData(RegimeDeTurno.Integral, "INTEGRAL")]
    public void ParaTokenCanonico_RoundTrip(RegimeDeTurno regime, string token)
    {
        RegimesDeTurno.ParaTokenCanonico(regime).Should().Be(token);
        RegimesDeTurno.TryAnalisar(token, out RegimeDeTurno resolvido).Should().BeTrue();
        resolvido.Should().Be(regime);
    }

    [Fact(DisplayName = "ParaTokenCanonico de Nenhum (sentinela) lança — não é regime válido")]
    public void ParaTokenCanonico_Nenhum_Lanca()
    {
        Action act = () => RegimesDeTurno.ParaTokenCanonico(RegimeDeTurno.Nenhum);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact(DisplayName = "TokensCanonicos lista exatamente os dois regimes")]
    public void TokensCanonicos_TemDoisValores()
    {
        RegimesDeTurno.TokensCanonicos.Should().HaveCount(2)
            .And.Contain(["REGULAR", "INTEGRAL"]);
    }

    [Theory(DisplayName = "REGULAR exige um turno; INTEGRAL exige dois")]
    [InlineData(RegimeDeTurno.Regular, 1)]
    [InlineData(RegimeDeTurno.Integral, 2)]
    public void TurnosExigidos_PorRegime(RegimeDeTurno regime, int esperado) =>
        RegimesDeTurno.TurnosExigidos(regime).Should().Be(esperado);

    [Fact(DisplayName = "TurnosExigidos do sentinela lança — não há cardinalidade sem regime")]
    public void TurnosExigidos_Nenhum_Lanca()
    {
        Action act = () => RegimesDeTurno.TurnosExigidos(RegimeDeTurno.Nenhum);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
