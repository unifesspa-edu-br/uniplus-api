namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// O parsing do regime de funcionamento é por allowlist textual explícita
/// (UNI-REQ-0138): só INTENSIVO e EXTENSIVO são aceitos. A obrigatoriedade do
/// regime é regra da entidade, não deste mapeamento.
/// </summary>
public sealed class RegimesDeFuncionamentoTests
{
    [Theory(DisplayName = "Os dois tokens canônicos são analisados para o regime correto")]
    [InlineData("INTENSIVO", RegimeDeFuncionamento.Intensivo)]
    [InlineData("EXTENSIVO", RegimeDeFuncionamento.Extensivo)]
    public void TryAnalisar_TokenCanonico_Resolve(string token, RegimeDeFuncionamento esperado)
    {
        RegimesDeFuncionamento.TryAnalisar(token, out RegimeDeFuncionamento regime).Should().BeTrue();
        regime.Should().Be(esperado);
    }

    [Fact(DisplayName = "Token com espaços é normalizado por Trim antes da resolução")]
    public void TryAnalisar_ComEspacos_Normaliza()
    {
        RegimesDeFuncionamento.TryAnalisar("  INTENSIVO  ", out RegimeDeFuncionamento regime).Should().BeTrue();
        regime.Should().Be(RegimeDeFuncionamento.Intensivo);
    }

    [Theory(DisplayName = "Tokens numéricos, PascalCase, de outra dimensão e vazios são rejeitados")]
    [InlineData("1")]
    [InlineData("Intensivo")]
    [InlineData("intensivo")]
    [InlineData("SEMI_INTENSIVO")]
    [InlineData("INTEGRAL")]    // regime de turno não é regime de funcionamento
    [InlineData("REGULAR")]     // idem — e REGULAR também é programa de oferta
    [InlineData("MATUTINO")]    // turno não é regime de funcionamento
    [InlineData("PRESENCIAL")]  // formato pedagógico não é regime de funcionamento
    [InlineData("")]
    [InlineData("   ")]
    public void TryAnalisar_ForaDoDominio_Rejeita(string token)
    {
        RegimesDeFuncionamento.TryAnalisar(token, out RegimeDeFuncionamento regime).Should().BeFalse();
        regime.Should().Be(RegimeDeFuncionamento.Nenhum);
        RegimesDeFuncionamento.EhValido(token).Should().BeFalse();
    }

    [Fact(DisplayName = "Token nulo é rejeitado sem lançar")]
    public void TryAnalisar_Nulo_Rejeita()
    {
        RegimesDeFuncionamento.TryAnalisar(null, out RegimeDeFuncionamento regime).Should().BeFalse();
        regime.Should().Be(RegimeDeFuncionamento.Nenhum);
    }

    [Theory(DisplayName = "ParaTokenCanonico é o inverso de TryAnalisar (round-trip)")]
    [InlineData(RegimeDeFuncionamento.Intensivo, "INTENSIVO")]
    [InlineData(RegimeDeFuncionamento.Extensivo, "EXTENSIVO")]
    public void ParaTokenCanonico_RoundTrip(RegimeDeFuncionamento regime, string token)
    {
        RegimesDeFuncionamento.ParaTokenCanonico(regime).Should().Be(token);
        RegimesDeFuncionamento.TryAnalisar(token, out RegimeDeFuncionamento resolvido).Should().BeTrue();
        resolvido.Should().Be(regime);
    }

    [Fact(DisplayName = "ParaTokenCanonico de Nenhum (sentinela) lança — não é regime válido")]
    public void ParaTokenCanonico_Nenhum_Lanca()
    {
        Action act = () => RegimesDeFuncionamento.ParaTokenCanonico(RegimeDeFuncionamento.Nenhum);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact(DisplayName = "TokensCanonicos lista exatamente os dois regimes")]
    public void TokensCanonicos_TemDoisValores()
    {
        RegimesDeFuncionamento.TokensCanonicos.Should().HaveCount(2)
            .And.Contain(["INTENSIVO", "EXTENSIVO"]);
    }

    [Fact(DisplayName = "INTENSIVO exige regime de turno INTEGRAL")]
    public void RegimeDeTurnoExigido_Intensivo_ExigeIntegral() =>
        RegimesDeFuncionamento.RegimeDeTurnoExigido(RegimeDeFuncionamento.Intensivo)
            .Should().Be(RegimeDeTurno.Integral);

    [Fact(DisplayName = "EXTENSIVO não restringe o regime de turno")]
    public void RegimeDeTurnoExigido_Extensivo_NaoRestringe() =>
        RegimesDeFuncionamento.RegimeDeTurnoExigido(RegimeDeFuncionamento.Extensivo)
            .Should().BeNull();

    [Fact(DisplayName = "RegimeDeTurnoExigido do sentinela lança — não há compatibilidade sem regime")]
    public void RegimeDeTurnoExigido_Nenhum_Lanca()
    {
        Action act = () => RegimesDeFuncionamento.RegimeDeTurnoExigido(RegimeDeFuncionamento.Nenhum);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
