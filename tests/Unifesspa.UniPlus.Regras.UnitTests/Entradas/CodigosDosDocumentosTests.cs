namespace Unifesspa.UniPlus.Regras.UnitTests.Entradas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;

public sealed class CodigosDosDocumentosTests
{
    [Fact]
    public void Ler_TipoDeReferencia_ConfereCodigo()
    {
        TipoDaDataDeReferenciaDaEntrada.Ler("FIM_DA_FASE").Value.Should().Be(TipoDaDataDeReferencia.FimDaFase);
        TipoDaDataDeReferenciaDaEntrada.Ler(null).Error!.Code.Should().Be(DataDeReferenciaDoDocumentoErrorCodes.TipoInvalido);
    }

    [Fact]
    public void Ler_Unidade_ConfereCodigo()
    {
        UnidadeDoPrazoDeEmissaoDaEntrada.Ler("MESES").Value.Should().Be(UnidadeDoPrazoDeEmissao.Meses);
        UnidadeDoPrazoDeEmissaoDaEntrada.Ler("SEMANAS").Error!.Code.Should().Be(PrazoDeEmissaoErrorCodes.UnidadeInvalida);
    }

    [Fact]
    public void Ler_Efeito_ConfereCodigo()
    {
        EfeitoNaAnaliseDoDocumentoDaEntrada.Ler("ACEITAR_COM_AVISO").Value.Should().Be(EfeitoNaAnaliseDoDocumento.AceitarComAviso);
        EfeitoNaAnaliseDoDocumentoDaEntrada.Ler("").Error!.Code.Should().Be(EfeitoNaAnaliseDoDocumentoErrorCodes.Invalido);
    }

    [Fact]
    public void Ler_EscolhaDoPrazo_ConfereCodigo()
    {
        EscolhaDoPrazoDeEmissaoDaEntrada.Ler("SEM_PRAZO").Value.Should().Be(EscolhaDoPrazoDeEmissao.SemPrazo);
        EscolhaDoPrazoDeEmissaoDaEntrada.Ler(null).Error!.Code.Should().Be(PrazoDeEmissaoErrorCodes.EscolhaInvalida);
    }

    [Fact]
    public void Ler_EscolhaDaValidade_ConfereCodigo()
    {
        EscolhaDaValidadeDaEntrada.Ler("EXIGE_DOCUMENTO_VALIDO").Value.Should().Be(EscolhaDaValidade.ExigeDocumentoValido);
        EscolhaDaValidadeDaEntrada.Ler(null).Error!.Code.Should().Be(ValidadeDoDocumentoErrorCodes.EscolhaInvalida);
    }
}
