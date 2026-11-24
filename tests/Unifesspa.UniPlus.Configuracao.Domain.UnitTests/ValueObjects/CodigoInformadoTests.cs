namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Regras.Enums;

public sealed class CodigoInformadoTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Ler_Ausente_NaoInformaValor(string? codigo)
    {
        CodigoInformado<TipoDoDado> resultado = CodigoInformado<TipoDoDado>.Ler(codigo);
        resultado.Informado.Should().BeFalse();
        resultado.Desconhecido.Should().BeFalse();
        resultado.Valor.Should().BeNull();
    }

    [Theory]
    [InlineData("DESCONHECIDO")]
    [InlineData("texto")]
    [InlineData(" TEXTO ")]
    public void Ler_CodigoNaoReconhecido_InformaDesconhecido(string codigo)
    {
        CodigoInformado<TipoDoDado> resultado = CodigoInformado<TipoDoDado>.Ler(codigo);
        resultado.Informado.Should().BeTrue();
        resultado.Desconhecido.Should().BeTrue();
        resultado.Valor.Should().BeNull();
    }

    [Fact]
    public void Ler_CodigoConhecido_InformaValor()
    {
        CodigoInformado<TipoDoDado> resultado = CodigoInformado<TipoDoDado>.Ler("TEXTO");
        resultado.Informado.Should().BeTrue();
        resultado.Desconhecido.Should().BeFalse();
        resultado.Valor.Should().Be(TipoDoDado.Texto);
    }

    [Fact]
    public void Converter_ValorConhecido_InformaValor()
    {
        CodigoInformado<TipoDoDado> resultado = TipoDoDado.Texto;
        resultado.Should().Be(CodigoInformado<TipoDoDado>.De(TipoDoDado.Texto));
    }

    [Fact]
    public void Converter_ValorNuloDoEnum_NaoInformaValor()
    {
        TipoDoDado? ausente = null;
        CodigoInformado<TipoDoDado> resultado = ausente;
        resultado.Informado.Should().BeFalse();
        resultado.Valor.Should().BeNull();
    }

    [Fact]
    public void Converter_ValorNuloLiteral_NaoInformaValor()
    {
        CodigoInformado<TipoDoDado> resultado = null;
        resultado.Informado.Should().BeFalse();
    }

    [Fact]
    public void De_ValorForaDoEnum_Recusa()
    {
        Func<CodigoInformado<TipoDoDado>> criar = static () => CodigoInformado<TipoDoDado>.De((TipoDoDado)(-1));
        criar.Should().Throw<ArgumentOutOfRangeException>();
    }
}
