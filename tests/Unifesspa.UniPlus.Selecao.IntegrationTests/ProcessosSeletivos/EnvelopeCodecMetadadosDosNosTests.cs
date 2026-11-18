namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

using static AdulteracaoDeEnvelopeDeTeste;

public sealed class EnvelopeCodecMetadadosDosNosTests
{
    [Theory]
    [InlineData(true, "O tipo de regra informado não é reconhecido. Corrija o tipo informado.")]
    [InlineData(false, "A forma de distinguir apresentações não é reconhecida. Corrija a forma de distinção na regra de documento.")]
    public void Reidratar_TipoOuDistincaoDesconhecidos_RecusaEmPortuguesClaro(bool tipoInvalido, string mensagem)
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            JsonObject folha = PrimeiroNo(envelope, TipoNoCodigo.Folha);
            if (tipoInvalido)
            {
                folha["tipo"] = "INVALIDO";
            }
            folha["chaveDistincao"] = "INVALIDA";
        }, comArvoreSatisfacao: true);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        resultado.Error.Message.Should().Contain(mensagem).And.NotContain("chaveDistincao").And.NotContain(".tipo");
    }

    [Fact]
    public void Reidratar_FolhaComConsequencia_RecusaEnvelopeMalformado()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(
            envelope => PrimeiroNo(envelope, TipoNoCodigo.Folha)["consequencia"] = "ELIMINA", comArvoreSatisfacao: true);

        ConferirRecusa(resultado, "consequencia");
    }

    [Fact]
    public void Reidratar_GrupoComDistincao_RecusaEnvelopeMalformado()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            JsonObject grupo = PrimeiroNo(envelope, TipoNoCodigo.GrupoOu);
            grupo["chaveDistincao"] = ChaveDistincaoCodigo.CompetenciaMensal;
            grupo["dataReferencia"] = "2026-10-10";
        }, comArvoreSatisfacao: true);

        ConferirRecusa(resultado, "chaveDistincao");
    }

    [Theory]
    [InlineData("consequencia", "basesLegais")]
    [InlineData("consequencia", "filhos")]
    [InlineData("basesLegais", "filhos")]
    [InlineData("filhos", null)]
    public void Reidratar_FolhaComAtributosIncompativeis_RecusaPrimeiroCampo(string primeiro, string? segundo)
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            JsonObject folha = PrimeiroNo(envelope, TipoNoCodigo.Folha);
            AdulterarFolha(folha, primeiro);
            if (segundo is not null)
            {
                AdulterarFolha(folha, segundo);
            }
        }, comArvoreSatisfacao: true);

        ConferirRecusa(resultado, primeiro);
    }

    [Theory]
    [InlineData(TipoNoCodigo.GrupoE, "chaveDistincao", "dataReferencia")]
    [InlineData(TipoNoCodigo.GrupoOu, "chaveDistincao", "ocorrenciasEsperadas")]
    [InlineData(TipoNoCodigo.GrupoE, "dataReferencia", "ocorrenciasEsperadas")]
    [InlineData(TipoNoCodigo.GrupoOu, "ocorrenciasEsperadas", null)]
    [InlineData(TipoNoCodigo.GrupoE, "ocorrenciasEsperadas", null)]
    public void Reidratar_GrupoComAtributosIncompativeis_RecusaPrimeiroCampo(string tipo, string primeiro, string? segundo)
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            JsonObject grupo = PrimeiroNo(envelope, TipoNoCodigo.GrupoOu);
            grupo["tipo"] = tipo;
            AdulterarGrupo(grupo, primeiro);
            if (segundo is not null)
            {
                AdulterarGrupo(grupo, segundo);
            }
        }, comArvoreSatisfacao: true);

        ConferirRecusa(resultado, primeiro);
    }

    [Fact]
    public void Reidratar_PaiEFilhoComAtributosIncompativeis_RecusaFilhoAntesDoPai()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            JsonObject grupo = PrimeiroNo(envelope, TipoNoCodigo.GrupoOu);
            grupo["dataReferencia"] = "2026-10-10";
            grupo["filhos"]![0]!["consequencia"] = "ELIMINA";
        }, comArvoreSatisfacao: true);

        ConferirRecusa(resultado, "consequencia");
    }

    [Fact]
    public void Reidratar_ReferenciaInexistenteEConsequenciaNaFolha_RecusaReferenciaAntesDaCompatibilidade()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            JsonObject folha = PrimeiroNo(envelope, TipoNoCodigo.Folha);
            folha["exigenciaId"] = Guid.CreateVersion7().ToString();
            folha["consequencia"] = "ELIMINA";
        }, comArvoreSatisfacao: true);

        ConferirRecusa(resultado, "exigenciaId");
    }

    [Fact]
    public void Reidratar_ComposicaoComDocumento_RecusaDocumento()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(
            envelope => PrimeiroNo(envelope, TipoNoCodigo.GrupoOu)["exigenciaId"] = Guid.CreateVersion7().ToString(), comArvoreSatisfacao: true);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Message.Should().Contain("A composição de regras não pode indicar um documento. Remova o documento deste item.");
    }

    [Fact]
    public void Reidratar_RegraSemDocumento_RecusaAusencia()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(
            envelope => PrimeiroNo(envelope, TipoNoCodigo.Folha)["exigenciaId"] = null, comArvoreSatisfacao: true);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Message.Should().Contain("A regra de documento precisa indicar um documento. Informe o documento exigido.");
    }

    private static void AdulterarFolha(JsonObject folha, string campo)
    {
        folha[campo] = campo switch
        {
            "consequencia" => JsonValue.Create("ELIMINA"),
            "basesLegais" => JsonNode.Parse("""[{"referencia":"Lei de referência","abrangencia":"FEDERAL","status":"RESOLVIDO","observacao":null}]"""),
            "filhos" => new JsonArray(FolhaSemAtributosIncompativeis(folha)),
            _ => throw new ArgumentOutOfRangeException(nameof(campo)),
        };
    }

    private static JsonObject FolhaSemAtributosIncompativeis(JsonObject folha)
    {
        JsonObject copia = folha.DeepClone().AsObject();
        copia["consequencia"] = null;
        copia["basesLegais"] = new JsonArray();
        copia["filhos"] = new JsonArray();
        return copia;
    }

    private static void AdulterarGrupo(JsonObject grupo, string campo)
    {
        grupo[campo] = campo switch
        {
            "chaveDistincao" => JsonValue.Create(ChaveDistincaoCodigo.CompetenciaMensal),
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
            "filhos" => "A regra de documento não pode conter outros itens. Remova os itens.",
            "chaveDistincao" => "A forma de distinguir apresentações só pode ser informada em uma regra de documento. Remova-a deste item.",
            "dataReferencia" => "A data de referência das apresentações só pode ser informada em uma regra de documento. Remova-a deste item.",
            "ocorrenciasEsperadas" => "As ocorrências esperadas só podem ser informadas em uma regra de documento. Remova-as deste item.",
            "exigenciaId" => "O documento indicado na regra não consta entre os documentos exigidos. Informe um documento exigido.",
            _ => throw new ArgumentOutOfRangeException(nameof(campo)),
        };
        resultado.Error.Message.Should().Contain(mensagem);
    }

    private static JsonObject PrimeiroNo(JsonObject envelope, string tipo) =>
        Nos(envelope["arvoreSatisfacao"]!.AsArray()).First(no => no["tipo"]!.GetValue<string>() == tipo);

    private static IEnumerable<JsonObject> Nos(JsonArray nos) =>
        nos.OfType<JsonObject>().SelectMany(static no => new[] { no }.Concat(Nos(no["filhos"]!.AsArray())));
}
