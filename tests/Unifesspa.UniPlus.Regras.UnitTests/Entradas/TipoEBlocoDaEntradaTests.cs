namespace Unifesspa.UniPlus.Regras.UnitTests.Entradas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Formularios;

public sealed class TipoEBlocoDaEntradaTests
{
    [Theory(DisplayName = "O código de cada tipo de etapa é lido como o tipo")]
    [InlineData("SECAO", TipoEtapaFormulario.Secao)]
    [InlineData("BLOCO", TipoEtapaFormulario.Bloco)]
    public void LerTipo_CodigoDeclarado_DevolveOTipo(string codigo, TipoEtapaFormulario esperado)
    {
        Result<TipoEtapaFormulario> lido = TipoEBlocoDaEntrada.LerTipo(codigo);

        lido.IsSuccess.Should().BeTrue();
        lido.Value.Should().Be(esperado);
    }

    [Theory(DisplayName = "O tipo ausente ou desconhecido é recusado como tipo obrigatório, sem repetir o valor recebido")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("secao")]
    [InlineData("SECAO ")]
    [InlineData("PAGINA")]
    public void LerTipo_AusenteOuDesconhecido_Recusa(string? codigo)
    {
        Result<TipoEtapaFormulario> lido = TipoEBlocoDaEntrada.LerTipo(codigo);

        lido.IsFailure.Should().BeTrue();
        lido.Error!.Code.Should().Be(EstruturaFormularioErrorCodes.TipoDeEtapaObrigatorio);
        lido.Error.Message.Should().Be("A etapa é uma seção ou um bloco de sistema. Os tipos aceitos são SECAO, BLOCO.");
    }

    [Theory(DisplayName = "O código de cada bloco é lido como o bloco")]
    [InlineData("COMPROVACAO_DOCUMENTAL", BlocoSistema.ComprovacaoDocumental)]
    [InlineData("MODALIDADES_CALCULADAS", BlocoSistema.ModalidadesCalculadas)]
    [InlineData("REVISAO_E_ACEITE", BlocoSistema.RevisaoEAceite)]
    public void LerBloco_CodigoDeclarado_DevolveOBloco(string codigo, BlocoSistema esperado)
    {
        Result<BlocoSistema?> lido = TipoEBlocoDaEntrada.LerBloco(codigo);

        lido.IsSuccess.Should().BeTrue();
        lido.Value.Should().Be(esperado);
    }

    [Fact(DisplayName = "O bloco ausente é a etapa sem bloco, e não uma recusa")]
    public void LerBloco_Ausente_EASecaoSemBloco()
    {
        Result<BlocoSistema?> lido = TipoEBlocoDaEntrada.LerBloco(null);

        lido.IsSuccess.Should().BeTrue();
        lido.Value.Should().BeNull();
    }

    [Theory(DisplayName = "O bloco desconhecido é recusado como não admitido, sem repetir o valor recebido")]
    [InlineData("")]
    [InlineData("revisao_e_aceite")]
    [InlineData("REVISAO_E_ACEITE ")]
    [InlineData("RODAPE")]
    public void LerBloco_Desconhecido_Recusa(string codigo)
    {
        Result<BlocoSistema?> lido = TipoEBlocoDaEntrada.LerBloco(codigo);

        lido.IsFailure.Should().BeTrue();
        lido.Error!.Code.Should().Be(EstruturaFormularioErrorCodes.BlocoNaoAdmitido);
        lido.Error.Message.Should().Be(
            "O bloco de sistema não é reconhecido. Os blocos aceitos são COMPROVACAO_DOCUMENTAL, MODALIDADES_CALCULADAS, REVISAO_E_ACEITE.");
    }
}
