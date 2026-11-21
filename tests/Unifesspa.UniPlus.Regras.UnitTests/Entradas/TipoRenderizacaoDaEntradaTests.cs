namespace Unifesspa.UniPlus.Regras.UnitTests.Entradas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;

public sealed class TipoRenderizacaoDaEntradaTests
{
    [Theory(DisplayName = "O código de cada tipo de campo é lido como o tipo")]
    [InlineData("NUMERO", TipoRenderizacao.Numero)]
    [InlineData("BOOLEANO", TipoRenderizacao.Booleano)]
    [InlineData("SELECAO_UNICA", TipoRenderizacao.SelecaoUnica)]
    [InlineData("SELECAO_MULTIPLA", TipoRenderizacao.SelecaoMultipla)]
    [InlineData("TEXTO", TipoRenderizacao.Texto)]
    [InlineData("DATA", TipoRenderizacao.Data)]
    [InlineData("ENDERECO", TipoRenderizacao.Endereco)]
    [InlineData("MUNICIPIO", TipoRenderizacao.Municipio)]
    public void Ler_CodigoDeclarado_DevolveOTipo(string codigo, TipoRenderizacao esperado)
    {
        Result<TipoRenderizacao> lido = TipoRenderizacaoDaEntrada.Ler(codigo);

        lido.IsSuccess.Should().BeTrue();
        lido.Value.Should().Be(esperado);
    }

    [Theory(DisplayName = "O tipo de campo ausente é recusado como obrigatório")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Ler_Ausente_RecusaComoObrigatorio(string? codigo)
    {
        Result<TipoRenderizacao> lido = TipoRenderizacaoDaEntrada.Ler(codigo);

        lido.IsFailure.Should().BeTrue();
        lido.Error!.Code.Should().Be(ItemFormularioErrorCodes.TipoRenderizacaoObrigatorio);
    }

    [Theory(DisplayName = "O tipo de campo desconhecido é recusado como inválido, sem repetir o valor recebido")]
    [InlineData("selecao_unica")]
    [InlineData("Numero")]
    [InlineData("SELECAO UNICA ")]
    [InlineData("NENHUMA")]
    public void Ler_Desconhecido_RecusaComoInvalidoListandoOsAceitos(string codigo)
    {
        Result<TipoRenderizacao> lido = TipoRenderizacaoDaEntrada.Ler(codigo);

        lido.IsFailure.Should().BeTrue();
        lido.Error!.Code.Should().Be(ItemFormularioErrorCodes.TipoRenderizacaoInvalido);
        lido.Error.Message.Should().Be(
            "O tipo de campo do item não é reconhecido. Os tipos aceitos são NUMERO, BOOLEANO, SELECAO_UNICA, SELECAO_MULTIPLA, TEXTO, DATA, ENDERECO, MUNICIPIO.");
    }
}
