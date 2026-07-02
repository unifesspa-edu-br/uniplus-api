namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// O parsing do turno é por allowlist textual explícita (#749): só os quatro
/// tokens canônicos UPPER_SNAKE são aceitos. A ausência de turno (nulo aceito)
/// é regra da entidade, não deste mapeamento.
/// </summary>
public sealed class TurnosOfertaTests
{
    [Theory(DisplayName = "Os quatro tokens canônicos são analisados para o turno correto")]
    [InlineData("MATUTINO", TurnoOferta.Matutino)]
    [InlineData("VESPERTINO", TurnoOferta.Vespertino)]
    [InlineData("NOTURNO", TurnoOferta.Noturno)]
    [InlineData("INTEGRAL", TurnoOferta.Integral)]
    public void TryAnalisar_TokenCanonico_Resolve(string token, TurnoOferta esperado)
    {
        TurnosOferta.TryAnalisar(token, out TurnoOferta turno).Should().BeTrue();
        turno.Should().Be(esperado);
    }

    [Fact(DisplayName = "Token com espaços é normalizado por Trim antes da resolução")]
    public void TryAnalisar_ComEspacos_Normaliza()
    {
        TurnosOferta.TryAnalisar("  NOTURNO  ", out TurnoOferta turno).Should().BeTrue();
        turno.Should().Be(TurnoOferta.Noturno);
    }

    [Theory(DisplayName = "Tokens numéricos, PascalCase, fora do domínio e vazios são rejeitados")]
    [InlineData("1")]           // numérico — Enum.TryParse aceitaria; a allowlist não
    [InlineData("4")]
    [InlineData("Matutino")]    // PascalCase do enum — não é o token de contrato
    [InlineData("Integral")]
    [InlineData("DIURNO")]      // fora do domínio fechado
    [InlineData("matutino")]    // case-sensitive
    [InlineData("")]
    [InlineData("   ")]
    public void TryAnalisar_ForaDoDominio_Rejeita(string token)
    {
        TurnosOferta.TryAnalisar(token, out TurnoOferta turno).Should().BeFalse();
        turno.Should().Be(TurnoOferta.Nenhum);
        TurnosOferta.EhValido(token).Should().BeFalse();
    }

    [Fact(DisplayName = "Token nulo é rejeitado sem lançar")]
    public void TryAnalisar_Nulo_Rejeita()
    {
        TurnosOferta.TryAnalisar(null, out TurnoOferta turno).Should().BeFalse();
        turno.Should().Be(TurnoOferta.Nenhum);
    }

    [Theory(DisplayName = "ParaTokenCanonico é o inverso de TryAnalisar (round-trip)")]
    [InlineData(TurnoOferta.Matutino, "MATUTINO")]
    [InlineData(TurnoOferta.Vespertino, "VESPERTINO")]
    [InlineData(TurnoOferta.Integral, "INTEGRAL")]
    public void ParaTokenCanonico_RoundTrip(TurnoOferta turno, string token)
    {
        TurnosOferta.ParaTokenCanonico(turno).Should().Be(token);
        TurnosOferta.TryAnalisar(token, out TurnoOferta resolvido).Should().BeTrue();
        resolvido.Should().Be(turno);
    }

    [Fact(DisplayName = "ParaTokenCanonico de Nenhum (sentinela) lança — não é turno válido")]
    public void ParaTokenCanonico_Nenhum_Lanca()
    {
        Action act = () => TurnosOferta.ParaTokenCanonico(TurnoOferta.Nenhum);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact(DisplayName = "TokensCanonicos lista exatamente os quatro valores de domínio")]
    public void TokensCanonicos_TemQuatroValores()
    {
        TurnosOferta.TokensCanonicos.Should().HaveCount(4)
            .And.Contain(["MATUTINO", "VESPERTINO", "NOTURNO", "INTEGRAL"]);
    }
}
