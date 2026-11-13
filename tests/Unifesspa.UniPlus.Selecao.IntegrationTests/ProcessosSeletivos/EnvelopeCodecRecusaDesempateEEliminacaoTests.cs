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
/// Os critérios de desempate por área do ENEM e as regras de eliminação da classificação: o que o decoder
/// recusa pela mesma regra da gravação — chave intrusa, repetição, área ou corte fora do quadro.
/// </summary>
public sealed class EnvelopeCodecRecusaDesempateEEliminacaoTests
{
    /// <summary>
    /// O critério de desempate por área do ENEM é o sexto da ordem no processo rico: é o
    /// índice 5 do array canônico, ordenado pela ordem do critério.
    /// </summary>
    private static JsonObject ArgsDoDesempatePorArea(JsonObject envelope)
    {
        JsonObject criterio = envelope["criteriosDesempate"]!.AsArray()[5]!.AsObject();
        criterio["regra"]!["codigo"]!.GetValue<string>().Should().Be(
            CriterioDesempateCodigo.MaiorNotaAreaEnem, "pré-condição: o índice 5 é o desempate por área do ENEM");
        return criterio["args"]!.AsObject();
    }

    [Fact(DisplayName = "criteriosDesempate[].args.areas que não é lista é recusado")]
    public void DesempatePorArea_AreasForaDeLista_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            ArgsDoDesempatePorArea(envelope)["areas"] = "REDACAO");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        resultado.Error.Message.Should().Contain("criteriosDesempate[5].args");
    }

    [Fact(DisplayName = "criteriosDesempate[].args.areas com item que não é texto é recusado")]
    public void DesempatePorArea_AreaQueNaoETexto_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            ArgsDoDesempatePorArea(envelope)["areas"]!.AsArray().Add(7));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        resultado.Error.Message.Should().Contain("criteriosDesempate[5].args.areas[3]");
    }

    [Fact(DisplayName = "criteriosDesempate[].args.areas vazia é recusada pela mesma regra da gravação")]
    public void DesempatePorArea_SemAreas_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            ArgsDoDesempatePorArea(envelope)["areas"] = new JsonArray());

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("CriterioDesempate.AreasObrigatorias");
    }

    [Fact(DisplayName = "criteriosDesempate[].args.areas com área repetida é recusada pela mesma regra da gravação")]
    public void DesempatePorArea_AreaRepetida_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            JsonArray areas = ArgsDoDesempatePorArea(envelope)["areas"]!.AsArray();
            areas.Add(areas[0]!.DeepClone());
        });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("CriterioDesempate.AreaRepetida");
    }

    [Fact(DisplayName = "criteriosDesempate[].args.areas com código fora da forma do cadastro é recusado")]
    public void DesempatePorArea_CodigoForaDaForma_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            ArgsDoDesempatePorArea(envelope)["areas"]!.AsArray()[0] = "redacao");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("CriterioDesempate.AreaInvalida");
    }

    [Fact(DisplayName = "classificacao.regrasEliminacao[].args da falta em dia de prova do ENEM com chave intrusa é recusado")]
    public void EliminacaoPorFaltaEmDiaDeProvaEnem_ChaveIntrusaNosArgs_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            JsonObject falta = Navegar(envelope, "classificacao.regrasEliminacao.4");
            falta["regra"]!["codigo"]!.GetValue<string>().Should().Be(
                RegraEliminacaoCodigo.ElimFaltaEmDiaDeProvaEnem, "pré-condição: o índice 4 é a eliminação por falta em dia de prova");
            falta["args"]!.AsObject()["chaveIntrusa"] = "x";
        });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        resultado.Error.Message.Should().Contain("classificacao.regrasEliminacao[4].args");
    }

    [Fact(DisplayName = "classificacao.regrasEliminacao[] com a falta em dia de prova do ENEM repetida é recusada pela mesma regra da gravação")]
    public void EliminacaoPorFaltaEmDiaDeProvaEnem_Repetida_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            JsonArray regras = Navegar(envelope, "classificacao")["regrasEliminacao"]!.AsArray();
            JsonNode falta = regras[4]!;
            falta["regra"]!["codigo"]!.GetValue<string>().Should().Be(
                RegraEliminacaoCodigo.ElimFaltaEmDiaDeProvaEnem, "pré-condição: o índice 4 é a eliminação por falta em dia de prova");
            regras.Insert(4, falta.DeepClone());
        });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoClassificacao.FaltaEmDiaDeProvaEnemRepetida");
    }

    private static JsonNode AusenciaNaEtapaDoEnvelope(JsonObject envelope) =>
        Navegar(envelope, "classificacao")["regrasEliminacao"]!.AsArray()
            .Single(regra => regra!["regra"]!["codigo"]!.GetValue<string>() == RegraEliminacaoCodigo.ElimAusenciaNaEtapa)!;

    [Fact(DisplayName = "classificacao.regrasEliminacao[].args da ausência na etapa com chave intrusa é recusado")]
    public void EliminacaoPorAusenciaNaEtapa_ChaveIntrusaNosArgs_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            AusenciaNaEtapaDoEnvelope(envelope)["args"]!.AsObject()["chaveIntrusa"] = "x");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        resultado.Error.Message.Should().Contain(".args");
    }

    [Fact(DisplayName = "classificacao.regrasEliminacao[] com a ausência na mesma etapa repetida é recusada pela mesma regra da gravação")]
    public void EliminacaoPorAusenciaNaEtapa_Repetida_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            JsonNode ausencia = AusenciaNaEtapaDoEnvelope(envelope);
            Navegar(envelope, "classificacao")["regrasEliminacao"]!.AsArray().Add(ausencia.DeepClone());
        });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoClassificacao.AusenciaNaEtapaRepetida");
    }

    [Fact(DisplayName = "classificacao.regrasEliminacao[] com corte em área fora do quadro é recusado pela mesma regra da gravação")]
    public void CorteEmArea_ForaDoQuadro_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            JsonObject corte = Navegar(envelope, "classificacao.regrasEliminacao.0");
            corte["regra"]!["codigo"]!.GetValue<string>().Should().Be(
                RegraEliminacaoCodigo.ElimCorteEmArea, "pré-condição: o índice 0 é o corte em área");
            corte["args"]!["areaCodigo"] = "FISICA";
        });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoClassificacao.CorteEmAreaForaDoQuadro");
    }

    [Fact(DisplayName = "classificacao.regrasEliminacao[] com dois cortes na mesma área é recusado pela mesma regra da gravação")]
    public void CorteEmArea_Repetido_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            JsonArray regras = Navegar(envelope, "classificacao")["regrasEliminacao"]!.AsArray();
            JsonNode corte = regras[0]!;
            corte["regra"]!["codigo"]!.GetValue<string>().Should().Be(
                RegraEliminacaoCodigo.ElimCorteEmArea, "pré-condição: o índice 0 é o corte em área");
            JsonNode outro = corte.DeepClone();
            outro["args"]!["minimo"] = "500.0000";
            regras.Insert(1, outro);
        });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoClassificacao.CorteEmAreaRepetido");
    }

    [Fact(DisplayName = "classificacao.regrasEliminacao[].args do corte em área sem a área é recusado")]
    public void CorteEmArea_SemArea_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            JsonObject corte = Navegar(envelope, "classificacao.regrasEliminacao.0");
            corte["regra"]!["codigo"]!.GetValue<string>().Should().Be(
                RegraEliminacaoCodigo.ElimCorteEmArea, "pré-condição: o índice 0 é o corte em área");
            corte["args"]!.AsObject().Remove("areaCodigo");
        });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        resultado.Error.Message.Should().Contain("classificacao.regrasEliminacao[0].args");
    }
}
