namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;

using Xunit;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.AdulteracaoDeEnvelopeDeTeste;

/// <summary>
/// A coleta de fatos, a derivação e o grafo conjunto (Story #928), e as classificações congeladas dos tipos
/// de ato e dos fatos derivados: o que o decoder recusa por ser forma ou vocabulário que o encoder nunca
/// emite.
/// </summary>
public sealed class EnvelopeCodecRecusaDerivacaoETiposDeAtoTests
{
    // ── Coleta de fatos / derivação / grafo conjunto (Story #928, §7.4) ──

    [Fact(DisplayName = "Versão do interpretador de derivação desconhecida é recusada")]
    public void VersaoInterpretadorDesconhecida_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(
            envelope => envelope["versaoInterpretador"] = "999");

        resultado.IsFailure.Should().BeTrue(
            "um snapshot resolvido por uma semântica de motor que este sistema não conhece não pode ser " +
            "reidratado como se fosse íntegro");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "Grafo conjunto congelado que não reproduz o recomputado é recusado (testemunho)")]
    public void GrafoTestemunhoDivergente_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            // Tira uma aresta do grafo congelado: o grafo recomputado das partes reidratadas ainda a
            // terá, então o testemunho não fecha — o congelado deixou de ser cópia verificável.
            JsonArray arestas = envelope["grafoDependencia"]!["arestas"]!.AsArray();
            arestas.RemoveAt(arestas.Count - 1);
        });

        resultado.IsFailure.Should().BeTrue(
            "o grafo congelado é testemunho redundante — se ele diverge do recomputado, a evidência não prova o que diz");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "Grafo conjunto congelado como null é recusado, não estoura em 500")]
    public void GrafoNulo_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(
            envelope => envelope["grafoDependencia"] = null);

        resultado.IsFailure.Should().BeTrue(
            "uma coluna adulterada com `grafoDependencia: null` e hash recomputado tem de recusar como malformada, " +
            "nunca produzir NullReferenceException");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Theory(DisplayName = "DNF vazio numa pré-condição ([] ou cláusula vazia [[]]) é recusado — o encoder emite null, nunca vazio")]
    [InlineData(false)]
    [InlineData(true)]
    public void DnfVazio_Recusa(bool clausulaVazia)
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            // RENDA é o fato com pré-condição; troca-a por um DNF vazio, que o decoder colapsaria para
            // "sem pré-condição" — uma forma que o encoder jamais emite (a ausência de condição é null).
            JsonObject renda = envelope["fatosColetados"]!.AsArray()
                .Single(fato => fato!["fatoCodigo"]!.GetValue<string>() == "RENDA")!.AsObject();
            renda["precondicao"] = clausulaVazia ? new JsonArray(new JsonArray()) : new JsonArray();
        });

        resultado.IsFailure.Should().BeTrue(
            "um DNF vazio colapsaria para 'sem pré-condição' e a restauração o recanonicalizaria como null, " +
            "aceitando uma forma que o certame nunca congelou");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "Impedimento em item de formulário que não é o de inscrição é recusado, como o agregado recusa")]
    public void ImpedimentoForaDaInscricao_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            FatoColetadoPorCodigo(envelope, "RENDA")["finalidade"] = "HABILITACAO");

        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        resultado.Error.Message.Should().Contain(".impedimento'");
    }

    [Fact(DisplayName = "A modalidade do candidato gravada entre as derivações do processo é recusada")]
    public void ModalidadeGravadaComoDerivacao_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["regrasDerivacao"]!.AsArray().Add(new JsonObject
            {
                ["codigoFato"] = "MODALIDADE",
                ["regras"] = new JsonArray(new JsonObject { ["ordem"] = 0, ["contribui"] = "AC", ["quando"] = null }),
            }));

        resultado.IsFailure.Should().BeTrue("a modalidade sai das modalidades ofertadas, e o encoder não a grava");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        resultado.Error.Message.Should().Contain("derivado da oferta");
    }

    [Fact(DisplayName = "Tipo de ato declarado no cronograma sem classificação congelada no envelope: recusa, e não 'não é único' por omissão")]
    public void TipoDeAtoDeclaradoSemClassificacaoCongelada_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["tiposDeAto"]!.AsArray().RemoveAt(0));

        resultado.IsFailure.Should().BeTrue(
            "sem a classificação, a publicação do item leria a ausência como 'não é resultado, não congela, não é único'");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "Classificação de tipo de ato sem uma das chaves no envelope: recusa")]
    public void TipoDeAtoSemUmaDasClassificacoes_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["tiposDeAto"]!.AsArray()[0]!.AsObject().Remove("congelaConfiguracao"));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "Fato derivado congelado com classificação fora do vocabulário: recusa, e não uma proteção inventada")]
    public void FatoDerivadoComClassificacaoForaDoVocabulario_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["fatosDerivados"]!.AsArray()[0]!.AsObject()["classificacaoProtecao"] = "SECRETO");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Theory(DisplayName = "Fato derivado congelado com domínio, cardinalidade ou hipótese legal fora do vocabulário: recusa")]
    [InlineData("dominio")]
    [InlineData("cardinalidade")]
    [InlineData("hipoteseLegal")]
    public void FatoDerivadoComTokenForaDoVocabulario_Recusa(string chave)
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["fatosDerivados"]!.AsArray()[0]!.AsObject()[chave] = "INVENTADO");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "Fato coletado congelado com hipótese legal fora do vocabulário: recusa")]
    public void FatoColetadoComHipoteseLegalForaDoVocabulario_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["fatosColetados"]!.AsArray()[0]!.AsObject()["hipoteseLegal"] = "INVENTADA");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "Fato derivado congelado sem a hipótese legal no envelope: recusa, e não 'sem hipótese' por omissão")]
    public void FatoDerivadoSemHipoteseLegal_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["fatosDerivados"]!.AsArray()[0]!.AsObject().Remove("hipoteseLegal"));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }
}
