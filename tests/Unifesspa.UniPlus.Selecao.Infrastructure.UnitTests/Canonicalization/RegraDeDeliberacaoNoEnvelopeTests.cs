namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using Xunit;

public sealed class RegraDeDeliberacaoNoEnvelopeTests
{
    [Theory]
    [InlineData("UNANIMIDADE", null, "FAVORAVEL")]
    [InlineData("MAIORIA_SIMPLES", "VOTO_DA_PRESIDENCIA", null)]
    public void SerializarELer_RegraComVotacao_PreservaOsCodigos(string criterio, string? desempate, string? semUnanimidade)
    {
        DeclaracaoDaDeliberacao declaracao = new("COLEGIADA_COM_VOTACAO", 3, 1, new(2, criterio, desempate, "PRESIDENCIA_NO_QUORUM", semUnanimidade));
        RegraDeDeliberacao original = RegraDeDeliberacao.Criar(declaracao).Value!;
        JsonObject banca = new() { ["regraDeDeliberacao"] = RegraDeDeliberacaoNoEnvelope.Serializar(original) };
        JsonObject bloco = banca["regraDeDeliberacao"]!.AsObject();
        LeitorEnvelope leitor = new();
        RegraDeDeliberacao? lida = RegraDeDeliberacaoNoEnvelope.Ler(leitor, JsonNode.Parse(banca.ToJsonString())!.AsObject(), "banca");

        string[] campos = ["modo", "criterio", "desempate", "encerramento", "semUnanimidade"];
        campos.Select(campo => bloco[campo]?.GetValue<string>()).Should().Equal(
            "COLEGIADA_COM_VOTACAO", criterio, desempate, "PRESIDENCIA_NO_QUORUM", semUnanimidade);
        leitor.Falhou.Should().BeFalse();
        lida.Should().BeEquivalentTo(original);
    }
}
