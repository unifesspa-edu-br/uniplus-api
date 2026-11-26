namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Services;

public sealed class FormasDoValorDoItemTests
{
    [Theory]
    [InlineData(TipoDeCampo.Texto, FormaDoValorDoItem.Texto)]
    [InlineData(TipoDeCampo.Numero, FormaDoValorDoItem.Numero)]
    [InlineData(TipoDeCampo.SimOuNao, FormaDoValorDoItem.SimOuNao)]
    [InlineData(TipoDeCampo.Data, FormaDoValorDoItem.Data)]
    [InlineData(TipoDeCampo.EscolhaUnica, FormaDoValorDoItem.Opcao)]
    [InlineData(TipoDeCampo.Municipio, FormaDoValorDoItem.Opcao)]
    [InlineData(TipoDeCampo.EscolhaMultipla, FormaDoValorDoItem.Opcoes)]
    [InlineData(TipoDeCampo.Endereco, FormaDoValorDoItem.Endereco)]
    public void De_Campo_RetornaForma(TipoDeCampo campo, FormaDoValorDoItem forma) =>
        FormasDoValorDoItem.De(campo).Should().Be(forma);

    [Theory]
    [InlineData(TipoDoDado.Texto, QuantidadeDeValores.ValorUnico, FormaDoValorDoItem.Texto)]
    [InlineData(TipoDoDado.Numerico, QuantidadeDeValores.ValorUnico, FormaDoValorDoItem.Numero)]
    [InlineData(TipoDoDado.SimOuNao, QuantidadeDeValores.ValorUnico, FormaDoValorDoItem.SimOuNao)]
    [InlineData(TipoDoDado.Data, QuantidadeDeValores.ValorUnico, FormaDoValorDoItem.Data)]
    [InlineData(TipoDoDado.Opcao, QuantidadeDeValores.ValorUnico, FormaDoValorDoItem.Opcao)]
    [InlineData(TipoDoDado.Opcao, QuantidadeDeValores.VariosValores, FormaDoValorDoItem.Opcoes)]
    [InlineData(TipoDoDado.Endereco, QuantidadeDeValores.ValorUnico, FormaDoValorDoItem.Endereco)]
    public void De_Dado_RetornaForma(TipoDoDado dado, QuantidadeDeValores quantidade, FormaDoValorDoItem forma) =>
        FormasDoValorDoItem.De(dado, quantidade).Should().Be(forma);

    [Fact]
    public void De_ValoresDesconhecidos_LancaExcecao()
    {
        FluentActions.Invoking(() => FormasDoValorDoItem.De((TipoDeCampo)99)).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => FormasDoValorDoItem.De((TipoDoDado)99, QuantidadeDeValores.ValorUnico)).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => FormasDoValorDoItem.De(TipoDoDado.Opcao, (QuantidadeDeValores)99)).Should().Throw<ArgumentOutOfRangeException>();
    }
}
