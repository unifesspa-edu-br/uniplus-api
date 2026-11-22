namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

using static AdulteracaoDeEnvelopeDeTeste;

public sealed class EnvelopeCodecMetadadosDasRegrasTests
{
    [Theory]
    [InlineData(true, "A forma de comprovação informada não é reconhecida. Corrija a forma informada.")]
    [InlineData(false, "A forma de distinguir apresentações não é reconhecida. Corrija a forma de distinção na regra de documento.")]
    public void Reidratar_FormaOuCriterioDesconhecidos_RecusaEmPortuguesClaro(bool formaInvalida, string mensagem)
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            JsonObject regraDeDocumento = PrimeiraRegra(envelope, "DOCUMENTO");
            if (formaInvalida)
            {
                regraDeDocumento["forma"] = "INVALIDO";
            }
            regraDeDocumento["criterioDeDistincao"] = "INVALIDA";
        }, comRegrasDeComprovacao: true);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        resultado.Error.Message.Should().Contain(mensagem).And.NotContain("criterioDeDistincao").And.NotContain(".forma");
    }

    [Fact]
    public void Reidratar_RegraDeDocumentoComConsequencia_RecusaEnvelopeMalformado()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(
            envelope => PrimeiraRegra(envelope, "DOCUMENTO")["consequencia"] = "ELIMINA", comRegrasDeComprovacao: true);

        ConferirRecusa(resultado, "consequencia");
    }

    [Fact]
    public void Reidratar_GrupoComDistincao_RecusaEnvelopeMalformado()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            JsonObject grupo = PrimeiraRegra(envelope, "QUANTIDADE_MINIMA_DE_ITENS");
            grupo["criterioDeDistincao"] = CriterioDeDistincaoDasApresentacoesCodigo.CompetenciaMensal;
            grupo["dataReferencia"] = "2026-10-10";
        }, comRegrasDeComprovacao: true);

        ConferirRecusa(resultado, "criterioDeDistincao");
    }

    [Theory]
    [InlineData("consequencia", "basesLegais")]
    [InlineData("consequencia", "itens")]
    [InlineData("basesLegais", "itens")]
    [InlineData("itens", null)]
    public void Reidratar_RegraDeDocumentoComAtributosIncompativeis_RecusaPrimeiroCampo(string primeiro, string? segundo)
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            JsonObject regraDeDocumento = PrimeiraRegra(envelope, "DOCUMENTO");
            AdulterarRegraDeDocumento(regraDeDocumento, primeiro);
            if (segundo is not null)
            {
                AdulterarRegraDeDocumento(regraDeDocumento, segundo);
            }
        }, comRegrasDeComprovacao: true);

        ConferirRecusa(resultado, primeiro);
    }

    [Theory]
    [InlineData("TODOS_OS_ITENS", "criterioDeDistincao", "dataReferencia")]
    [InlineData("QUANTIDADE_MINIMA_DE_ITENS", "criterioDeDistincao", "ocorrenciasEsperadas")]
    [InlineData("TODOS_OS_ITENS", "dataReferencia", "ocorrenciasEsperadas")]
    [InlineData("QUANTIDADE_MINIMA_DE_ITENS", "ocorrenciasEsperadas", null)]
    [InlineData("TODOS_OS_ITENS", "ocorrenciasEsperadas", null)]
    public void Reidratar_GrupoComAtributosIncompativeis_RecusaPrimeiroCampo(string forma, string primeiro, string? segundo)
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            JsonObject grupo = PrimeiraRegra(envelope, "QUANTIDADE_MINIMA_DE_ITENS");
            grupo["forma"] = forma;
            AdulterarGrupo(grupo, primeiro);
            if (segundo is not null)
            {
                AdulterarGrupo(grupo, segundo);
            }
        }, comRegrasDeComprovacao: true);

        ConferirRecusa(resultado, primeiro);
    }

    [Fact]
    public void Reidratar_GrupoEItemComAtributosIncompativeis_RecusaItemAntesDoGrupo()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            JsonObject grupo = PrimeiraRegra(envelope, "QUANTIDADE_MINIMA_DE_ITENS");
            grupo["dataReferencia"] = "2026-10-10";
            grupo["itens"]![0]!["consequencia"] = "ELIMINA";
        }, comRegrasDeComprovacao: true);

        ConferirRecusa(resultado, "consequencia");
    }

    [Fact]
    public void Reidratar_ReferenciaInexistenteEConsequenciaNaRegraDeDocumento_RecusaReferenciaAntesDaCompatibilidade()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            JsonObject regraDeDocumento = PrimeiraRegra(envelope, "DOCUMENTO");
            regraDeDocumento["exigenciaId"] = Guid.CreateVersion7().ToString();
            regraDeDocumento["consequencia"] = "ELIMINA";
        }, comRegrasDeComprovacao: true);

        ConferirRecusa(resultado, "exigenciaId");
    }

    [Fact]
    public void Reidratar_ComposicaoComDocumento_RecusaDocumento()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(
            envelope => PrimeiraRegra(envelope, "QUANTIDADE_MINIMA_DE_ITENS")["exigenciaId"] = Guid.CreateVersion7().ToString(), comRegrasDeComprovacao: true);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Message.Should().Contain("A composição de regras não pode indicar um documento. Remova o documento deste item.");
    }

    [Fact]
    public void Reidratar_RegraSemDocumento_RecusaAusencia()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(
            envelope => PrimeiraRegra(envelope, "DOCUMENTO")["exigenciaId"] = null, comRegrasDeComprovacao: true);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Message.Should().Contain("A regra de documento precisa indicar um documento. Informe o documento exigido.");
    }

    private static void AdulterarRegraDeDocumento(JsonObject regraDeDocumento, string campo)
    {
        regraDeDocumento[campo] = campo switch
        {
            "consequencia" => JsonValue.Create("ELIMINA"),
            "basesLegais" => JsonNode.Parse("""[{"referencia":"Lei de referência","abrangencia":"FEDERAL","situacao":"RESOLVIDO","observacao":null}]"""),
            "itens" => new JsonArray(RegraDeDocumentoSemAtributosIncompativeis(regraDeDocumento)),
            _ => throw new ArgumentOutOfRangeException(nameof(campo)),
        };
    }

    private static JsonObject RegraDeDocumentoSemAtributosIncompativeis(JsonObject regraDeDocumento)
    {
        JsonObject copia = regraDeDocumento.DeepClone().AsObject();
        copia["consequencia"] = null;
        copia["basesLegais"] = new JsonArray();
        copia["itens"] = new JsonArray();
        return copia;
    }

    private static void AdulterarGrupo(JsonObject grupo, string campo)
    {
        grupo[campo] = campo switch
        {
            "criterioDeDistincao" => JsonValue.Create(CriterioDeDistincaoDasApresentacoesCodigo.CompetenciaMensal),
            "dataReferencia" => JsonValue.Create("2026-10-10"),
            "ocorrenciasEsperadas" => new JsonArray(),
            _ => throw new ArgumentOutOfRangeException(nameof(campo)),
        };
    }

    private static void ConferirRecusa(Result<EnvelopeReidratado> resultado, string campo)
    {
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        string mensagem = campo switch
        {
            "consequencia" => "A regra de documento não pode ter consequência própria. Remova a consequência.",
            "basesLegais" => "A regra de documento não pode ter base legal própria. Remova a base legal.",
            "itens" => "A regra de documento não pode conter outros itens. Remova os itens.",
            "criterioDeDistincao" => "A forma de distinguir apresentações só pode ser informada em uma regra de documento. Remova-a deste item.",
            "dataReferencia" => "A data de referência das apresentações só pode ser informada em uma regra de documento. Remova-a deste item.",
            "ocorrenciasEsperadas" => "As ocorrências esperadas só podem ser informadas em uma regra de documento. Remova-as deste item.",
            "exigenciaId" => "O documento indicado na regra não consta entre os documentos exigidos. Informe um documento exigido.",
            _ => throw new ArgumentOutOfRangeException(nameof(campo)),
        };
        resultado.Error.Message.Should().Contain(mensagem);
    }

    private static JsonObject PrimeiraRegra(JsonObject envelope, string forma) =>
        Regras(envelope["regrasDeComprovacao"]!.AsArray()).First(regra => regra["forma"]!.GetValue<string>() == forma);

    private static IEnumerable<JsonObject> Regras(JsonArray regras) =>
        regras.OfType<JsonObject>().SelectMany(static regra => new[] { regra }.Concat(Regras(regra["itens"]!.AsArray())));
}
