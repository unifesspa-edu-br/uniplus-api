namespace Unifesspa.UniPlus.Regras.UnitTests.Entradas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;

public sealed class TipoDeCampoDaEntradaTests
{
    [Theory(DisplayName = "O código de cada tipo de campo é lido como o tipo")]
    [InlineData("NUMERO", TipoDeCampo.Numero)]
    [InlineData("SIM_OU_NAO", TipoDeCampo.SimOuNao)]
    [InlineData("ESCOLHA_UNICA", TipoDeCampo.EscolhaUnica)]
    [InlineData("ESCOLHA_MULTIPLA", TipoDeCampo.EscolhaMultipla)]
    [InlineData("TEXTO", TipoDeCampo.Texto)]
    [InlineData("DATA", TipoDeCampo.Data)]
    [InlineData("ENDERECO", TipoDeCampo.Endereco)]
    [InlineData("MUNICIPIO", TipoDeCampo.Municipio)]
    public void Ler_CodigoDeclarado_DevolveOTipo(string codigo, TipoDeCampo esperado)
    {
        Result<TipoDeCampo> lido = TipoDeCampoDaEntrada.Ler(codigo);

        lido.IsSuccess.Should().BeTrue();
        lido.Value.Should().Be(esperado);
    }

    [Theory(DisplayName = "O tipo de campo ausente é recusado como obrigatório")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Ler_Ausente_RecusaComoObrigatorio(string? codigo)
    {
        Result<TipoDeCampo> lido = TipoDeCampoDaEntrada.Ler(codigo);

        lido.IsFailure.Should().BeTrue();
        lido.Error!.Code.Should().Be(ItemFormularioErrorCodes.TipoDeCampoObrigatorio);
    }

    [Theory(DisplayName = "O tipo de campo desconhecido é recusado como inválido, sem repetir o valor recebido")]
    [InlineData("selecao_unica")]
    [InlineData("Numero")]
    [InlineData("SELECAO UNICA ")]
    [InlineData("NENHUMA")]
    public void Ler_Desconhecido_RecusaComoInvalidoListandoOsAceitos(string codigo)
    {
        Result<TipoDeCampo> lido = TipoDeCampoDaEntrada.Ler(codigo);

        lido.IsFailure.Should().BeTrue();
        lido.Error!.Code.Should().Be(ItemFormularioErrorCodes.TipoDeCampoInvalido);
        lido.Error.Message.Should().Be(
            "O tipo de campo do item não é reconhecido. Os tipos aceitos são NUMERO, SIM_OU_NAO, ESCOLHA_UNICA, ESCOLHA_MULTIPLA, TEXTO, DATA, ENDERECO, MUNICIPIO.");
    }
}
