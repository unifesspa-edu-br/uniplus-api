namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

public sealed class CodigosDeCadastroTests
{
    [Theory]
    [InlineData("A")]
    [InlineData("FASE2")]
    [InlineData("LEI_12711")]
    public void EhValido_CodigosValidos_Aceita(string codigo)
    {
        CodigoFase.EhValido(codigo).Should().BeTrue();
        CodigoFatoCandidato.EhValido(codigo).Should().BeTrue();
        CodigoOfertaCurso.EhValido(codigo).Should().BeTrue();
        CodigoTipoDocumento.EhValido(codigo).Should().BeTrue();
    }

    [Theory]
    [InlineData(" X ")]
    [InlineData("A__B")]
    [InlineData("A_")]
    [InlineData("1X")]
    public void EhValido_CodigosInvalidos_Recusa(string codigo)
    {
        CodigoFase.EhValido(codigo).Should().BeFalse();
        CodigoFatoCandidato.EhValido(codigo).Should().BeFalse();
        CodigoOfertaCurso.EhValido(codigo).Should().BeFalse();
        CodigoTipoDocumento.EhValido(codigo).Should().BeFalse();
    }
}
