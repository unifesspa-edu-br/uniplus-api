namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;

public sealed class NaturezasConceitoTests
{
    [Theory(DisplayName = "O token do banco e o nome no contrato identificam a mesma natureza")]
    [InlineData(NaturezaConceito.Numerico, "NUMERICO", "numerico")]
    [InlineData(NaturezaConceito.Categorico, "CATEGORICO", "categorico")]
    public void Natureza_TemTokenDoBancoENomeNoContrato(NaturezaConceito natureza, string token, string contrato)
    {
        NaturezasConceito.ParaToken(natureza).Should().Be(token);
        NaturezasConceito.ParaContrato(natureza).Should().Be(contrato);
        NaturezasConceito.TryAnalisarToken(token, out NaturezaConceito doToken).Should().BeTrue();
        NaturezasConceito.TryAnalisarContrato(contrato, out NaturezaConceito doContrato).Should().BeTrue();
        doToken.Should().Be(natureza);
        doContrato.Should().Be(natureza);
    }

    [Theory(DisplayName = "O contrato recusa o que não é o nome exato de uma natureza")]
    [InlineData("NUMERICO")]
    [InlineData("Numerico")]
    [InlineData("1")]
    [InlineData("ordinal")]
    [InlineData("")]
    [InlineData(null)]
    public void Contrato_ForaDoDominio_Recusa(string? valor)
    {
        NaturezasConceito.TryAnalisarContrato(valor, out NaturezaConceito natureza).Should().BeFalse();
        natureza.Should().Be(NaturezaConceito.Nenhuma);
    }

    [Theory(DisplayName = "O banco recusa o que não é o token exato de uma natureza")]
    [InlineData("numerico")]
    [InlineData("0")]
    [InlineData(null)]
    public void Token_ForaDoDominio_Recusa(string? valor)
    {
        NaturezasConceito.TryAnalisarToken(valor, out NaturezaConceito natureza).Should().BeFalse();
        natureza.Should().Be(NaturezaConceito.Nenhuma);
    }

    [Fact(DisplayName = "Natureza sentinela não tem token")]
    public void Sentinela_NaoTemToken()
    {
        Action act = () => NaturezasConceito.ParaToken(NaturezaConceito.Nenhuma);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact(DisplayName = "Os tokens canônicos alimentam o CHECK de domínio")]
    public void TokensCanonicos_SaoAsDuasNaturezas() =>
        NaturezasConceito.TokensCanonicos.Should().Equal("NUMERICO", "CATEGORICO");
}
