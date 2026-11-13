namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;

using Xunit;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.AdulteracaoDeEnvelopeDeTeste;

/// <summary>
/// A taxa de inscrição e a isenção (issue #1112): o bloco <c>taxaInscricao</c> é de forma fechada e de
/// vocabulário fechado, como o de divulgação, e o decoder é tão estrito quanto a escrita.
/// </summary>
public sealed class EnvelopeCodecRecusaTaxaDeInscricaoTests
{
    [Fact(DisplayName = "issue #1112: taxaInscricao.presente=false é recusado — CA-01 garante que todo envelope publicado declarou taxa")]
    public void TaxaInscricao_PresenteFalso_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            JsonObject bloco = envelope["taxaInscricao"]!.AsObject();
            bloco.Clear();
            bloco["presente"] = false;
        });

        resultado.IsFailure.Should().BeTrue(
            "um envelope legitimamente publicado nunca congela 'presente:false' — CA-01 recusa Publicar() " +
            "sem taxa declarada, então só bytes adulterados chegam ao decoder nesse estado");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        resultado.Error!.Message.Should().Contain("taxaInscricao.presente");
    }

    [Fact(DisplayName = "issue #1112: fundamento de isenção com token fora do vocabulário conhecido é recusado")]
    public void TaxaInscricao_FundamentoDesconhecido_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["taxaInscricao"]!["fundamentos"] = new JsonArray(JsonValue.Create("FUNDAMENTO_INEXISTENTE")));

        resultado.IsFailure.Should().BeTrue(
            "'FUNDAMENTO_INEXISTENTE' não pertence ao vocabulário de FundamentoIsencaoCodigo — a factory recusa, " +
            "nunca ignora em silêncio");
        resultado.Error!.Code.Should().Be("ConfiguracaoTaxaInscricao.FundamentoDesconhecido");
    }

    [Fact(DisplayName = "issue #1112: fundamentos repetidos são recusados — o encoder nunca emite duplicata")]
    public void TaxaInscricao_FundamentosDuplicados_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["taxaInscricao"]!["fundamentos"] = new JsonArray(
                JsonValue.Create(FundamentoIsencaoCodigo.CadastroUnico), JsonValue.Create(FundamentoIsencaoCodigo.CadastroUnico)));

        resultado.IsFailure.Should().BeTrue(
            "ConfiguracaoTaxaInscricao.Criar deduplica antes de guardar — o encoder nunca emite token repetido; " +
            "achar um é sinal de bytes que não vieram desse caminho");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        resultado.Error!.Message.Should().Contain("taxaInscricao.fundamentos");
    }

    [Fact(DisplayName = "issue #1112: fundamentos fora da ordem canônica são recusados — o decoder não reordena")]
    public void TaxaInscricao_FundamentosForaDeOrdem_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["taxaInscricao"]!["fundamentos"] = new JsonArray(
                JsonValue.Create(FundamentoIsencaoCodigo.DoacaoMedulaOssea), JsonValue.Create(FundamentoIsencaoCodigo.CadastroUnico)));

        resultado.IsFailure.Should().BeTrue(
            "'fundamentos' sai do encoder na ordem canônica (código ordinal) porque a entidade já os guarda " +
            "ordenados — fora de ordem é malformado, não uma forma alternativa a reordenar em silêncio");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        resultado.Error!.Message.Should().Contain("taxaInscricao.fundamentos");
    }

    [Fact(DisplayName = "issue #1319: envelope que ainda traz confirmacaoFundamentos é recusado — o bloco é de forma fechada")]
    public void TaxaInscricao_ChaveConfirmacaoFundamentosRemovida_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["taxaInscricao"]!["confirmacaoFundamentos"] = false);

        resultado.IsFailure.Should().BeTrue(
            "a confirmação saiu do agregado e da emissão — chave a mais no bloco é malformado, " +
            "não campo tolerado por compatibilidade");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        resultado.Error!.Message.Should().Contain("confirmacaoFundamentos",
            "a recusa nomeia a chave intrusa — sem isso o teste passaria por qualquer defeito " +
            "de forma no bloco, inclusive um alheio à remoção");
    }

    [Fact(DisplayName = "issue #1310: envelope que cobra taxa sem nenhum fundamento é recusado — o decoder é tão estrito quanto a escrita")]
    public void TaxaInscricao_CobraSemFundamento_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["taxaInscricao"]!["fundamentos"] = new JsonArray());

        resultado.IsFailure.Should().BeTrue(
            "ConfiguracaoTaxaInscricao.Criar exige fundamento de quem cobra — o encoder nunca " +
            "emite 'cobra:true' com 'fundamentos:[]', e o decoder não pode aceitar o que a escrita recusa");
        resultado.Error!.Code.Should().Be("ConfiguracaoTaxaInscricao.FundamentoObrigatorioQuandoCobra");
    }
}
