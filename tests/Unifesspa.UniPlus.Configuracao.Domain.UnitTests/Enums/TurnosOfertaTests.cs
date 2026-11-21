namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// O parsing do turno é por lista textual explícita de códigos aceitos: só os três códigos
/// canônicos UPPER_SNAKE são aceitos. Quantos turnos a oferta declara é regra da
/// entidade, decidida pelo regime, não deste mapeamento.
/// </summary>
public sealed class TurnosOfertaTests
{
    [Theory(DisplayName = "Os três códigos canônicos são analisados para o turno correto")]
    [InlineData("MATUTINO", TurnoOferta.Matutino)]
    [InlineData("VESPERTINO", TurnoOferta.Vespertino)]
    [InlineData("NOTURNO", TurnoOferta.Noturno)]
    public void TryAnalisar_CodigoCanonico_Resolve(string codigo, TurnoOferta esperado)
    {
        TurnosOferta.TryAnalisar(codigo, out TurnoOferta turno).Should().BeTrue();
        turno.Should().Be(esperado);
    }

    [Fact(DisplayName = "Código com espaços é normalizado por Trim antes da resolução")]
    public void TryAnalisar_ComEspacos_Normaliza()
    {
        TurnosOferta.TryAnalisar("  NOTURNO  ", out TurnoOferta turno).Should().BeTrue();
        turno.Should().Be(TurnoOferta.Noturno);
    }

    [Fact(DisplayName = "INTEGRAL deixou de ser turno — virou regime, e o código é rejeitado aqui")]
    public void TryAnalisar_Integral_Rejeita()
    {
        TurnosOferta.TryAnalisar("INTEGRAL", out TurnoOferta turno).Should().BeFalse();
        turno.Should().Be(TurnoOferta.Nenhum);
        TurnosOferta.EhValido("INTEGRAL").Should().BeFalse();
    }

    [Theory(DisplayName = "Códigos numéricos, PascalCase, fora do domínio e vazios são rejeitados")]
    [InlineData("1")]           // numérico — Enum.TryParse aceitaria; a lista de códigos aceitos não
    [InlineData("4")]
    [InlineData("Matutino")]    // PascalCase do enum — não é o código de contrato
    [InlineData("DIURNO")]      // fora do domínio fechado
    [InlineData("matutino")]    // case-sensitive
    [InlineData("")]
    [InlineData("   ")]
    public void TryAnalisar_ForaDoDominio_Rejeita(string codigo)
    {
        TurnosOferta.TryAnalisar(codigo, out TurnoOferta turno).Should().BeFalse();
        turno.Should().Be(TurnoOferta.Nenhum);
        TurnosOferta.EhValido(codigo).Should().BeFalse();
    }

    [Fact(DisplayName = "Código nulo é rejeitado sem lançar")]
    public void TryAnalisar_Nulo_Rejeita()
    {
        TurnosOferta.TryAnalisar(null, out TurnoOferta turno).Should().BeFalse();
        turno.Should().Be(TurnoOferta.Nenhum);
    }

    [Theory(DisplayName = "ParaTokenCanonico é o inverso de TryAnalisar (round-trip)")]
    [InlineData(TurnoOferta.Matutino, "MATUTINO")]
    [InlineData(TurnoOferta.Vespertino, "VESPERTINO")]
    [InlineData(TurnoOferta.Noturno, "NOTURNO")]
    public void ParaCodigoCanonico_IdaEVolta(TurnoOferta turno, string codigo)
    {
        TurnosOferta.ParaTokenCanonico(turno).Should().Be(codigo);
        TurnosOferta.TryAnalisar(codigo, out TurnoOferta resolvido).Should().BeTrue();
        resolvido.Should().Be(turno);
    }

    [Fact(DisplayName = "ParaTokenCanonico de Nenhum (valor de ausência) lança — não é turno válido")]
    public void ParaTokenCanonico_Nenhum_Lanca()
    {
        Action act = () => TurnosOferta.ParaTokenCanonico(TurnoOferta.Nenhum);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact(DisplayName = "TokensCanonicos lista exatamente os três períodos do dia")]
    public void TokensCanonicos_TemTresValores()
    {
        TurnosOferta.TokensCanonicos.Should().HaveCount(3)
            .And.Contain(["MATUTINO", "VESPERTINO", "NOTURNO"]);
    }

    [Fact(DisplayName = "A ordem canônica dos turnos é a do enum: matutino, vespertino, noturno")]
    public void OrdemCanonica_SegueOEnum()
    {
        List<TurnoOferta> turnos = [TurnoOferta.Noturno, TurnoOferta.Matutino, TurnoOferta.Vespertino];
        turnos.Sort();

        turnos.Should().Equal(TurnoOferta.Matutino, TurnoOferta.Vespertino, TurnoOferta.Noturno);
    }
}
