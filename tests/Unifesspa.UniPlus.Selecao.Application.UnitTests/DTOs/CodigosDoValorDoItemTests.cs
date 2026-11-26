namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.DTOs;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Application.DTOs;

public sealed class CodigosDoValorDoItemTests
{
    [Theory]
    [InlineData(FormaDoValorDoItem.Texto, "TEXTO")]
    [InlineData(FormaDoValorDoItem.Numero, "NUMERO")]
    [InlineData(FormaDoValorDoItem.SimOuNao, "SIM_OU_NAO")]
    [InlineData(FormaDoValorDoItem.Data, "DATA")]
    [InlineData(FormaDoValorDoItem.Opcao, "OPCAO")]
    [InlineData(FormaDoValorDoItem.Opcoes, "OPCOES")]
    [InlineData(FormaDoValorDoItem.Endereco, "ENDERECO")]
    public void Codigo_FormaDoValor_CorrespondeAoContrato(FormaDoValorDoItem valor, string codigo) =>
        valor.Codigo().Should().Be(codigo);

    [Theory]
    [InlineData(ExibicaoDoValorDoItem.Padrao, "PADRAO")]
    [InlineData(ExibicaoDoValorDoItem.Cpf, "CPF")]
    [InlineData(ExibicaoDoValorDoItem.Identificador, "IDENTIFICADOR")]
    [InlineData(ExibicaoDoValorDoItem.Moeda, "MOEDA")]
    [InlineData(ExibicaoDoValorDoItem.Situacao, "SITUACAO")]
    public void Codigo_ExibicaoDoValor_CorrespondeAoContrato(ExibicaoDoValorDoItem valor, string codigo) =>
        valor.Codigo().Should().Be(codigo);
}
