namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

public sealed class CausasDasRestricoesNoEnvelopeTests
{
    [Theory]
    [InlineData(false, true)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public void LerRestricoes_CondicoesDasOpcoesInvalidas_PropagaCausas(bool separadas, bool unica)
    {
        JsonObject entrada = new() { ["quando"] = CondicoesNoEnvelope.Invalidas(separadas, unica), ["valores"] = new JsonArray("A") };
        JsonObject restricao = new() { ["tipo"] = "OPCOES_PERMITIDAS", ["entradas"] = new JsonArray(entrada) };
        JsonObject item = new() { ["restricoes"] = new JsonArray(restricao) };
        LeitorEnvelope leitor = new();
        LeituraDasRestricoes.LerRestricoes(leitor, item, "item");
        CondicoesNoEnvelope.Conferir(new JsonObject { ["item"] = item }, leitor, "item.restricoes[0].entradas[0].quando", separadas, unica);
    }

    [Fact]
    public void LerRestricoes_LimitesDeDataInvalidos_SituaAmbasAsCausas()
    {
        JsonObject restricao = new() { ["tipo"] = "FAIXA_DE_DATA", ["dataMinima"] = Limite(), ["dataMaxima"] = Limite() };
        JsonObject item = new() { ["restricoes"] = new JsonArray(restricao) };
        LeitorEnvelope leitor = new();
        LeituraDasRestricoes.LerRestricoes(leitor, item, "item");
        IReadOnlyList<FieldError> causas = leitor.Falha<object>().Errors;
        causas.Should().HaveCount(2);
        ConferirLimite(new JsonObject { ["item"] = item }, causas[0], "item.restricoes[0].dataMinima");
        ConferirLimite(new JsonObject { ["item"] = item.DeepClone() }, causas[1], "item.restricoes[0].dataMaxima");
    }

    private static JsonObject Limite() => new() { ["referenciaDaData"] = "ENVIO", ["anos"] = 151, ["dias"] = 0 };

    private static void ConferirLimite(JsonObject documento, FieldError causa, string campo)
    {
        causa.Field.Should().Be(campo);
        causa.Error.Code.Should().Be("RestricaoValor.LimiteDeDataInvalido");
        causa.Error.Message.Should().Be("O deslocamento do dia do envio vai até 150 anos ou 54900 dias, para mais ou para menos.");
        CaminhoNoDocumento.Existe(documento, campo).Should().BeTrue();
    }
}
