namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using Xunit;

public sealed class RegraDeDeliberacaoNoEnvelopeTests
{
    [Theory]
    [InlineData("unanimidade", null, "favoravel")]
    [InlineData("maioriaSimples", "votoDaPresidencia", null)]
    public void SerializarELer_RegraComVotacao_PreservaOsCodigos(string criterio, string? desempate, string? semUnanimidade)
    {
        DeclaracaoDaDeliberacao declaracao = new("colegiadaComVotacao", 3, 1, new(2, criterio, desempate, "presidenciaNoQuorum", semUnanimidade));
        RegraDeDeliberacao original = RegraDeDeliberacao.Criar(declaracao).Value!;
        JsonObject banca = new() { ["regraDeDeliberacao"] = RegraDeDeliberacaoNoEnvelope.Serializar(original) };
        JsonObject bloco = banca["regraDeDeliberacao"]!.AsObject();
        LeitorEnvelope leitor = new();
        RegraDeDeliberacao? lida = RegraDeDeliberacaoNoEnvelope.Ler(leitor, JsonNode.Parse(banca.ToJsonString())!.AsObject(), "banca");

        string[] campos = ["modo", "criterio", "desempate", "encerramento", "semUnanimidade"];
        campos.Select(campo => bloco[campo]?.GetValue<string>()).Should().Equal(
            "colegiadaComVotacao", criterio, desempate, "presidenciaNoQuorum", semUnanimidade);
        leitor.Falhou.Should().BeFalse();
        lida.Should().BeEquivalentTo(original);
    }
}
