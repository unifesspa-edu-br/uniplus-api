namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

public sealed class ChavesNoEnvelopeTests
{
    [Theory]
    [InlineData("fase_inscricao")]
    [InlineData("etapa_prova_2")]
    public void Ler_ChaveValida_PreservaOValor(string texto)
    {
        LeitorEnvelope leitor = new();
        ChaveLocal chave = ChavesNoEnvelope.Ler(leitor, new JsonObject { ["chave"] = texto }, "parte");

        leitor.Falhou.Should().BeFalse();
        chave.Valor.Should().Be(texto);
    }

    [Theory]
    [InlineData("Etapa")]
    [InlineData("etapa__x")]
    [InlineData("etapa-")]
    [InlineData("")]
    public void Ler_ChaveMalformada_Recusa(string texto)
    {
        LeitorEnvelope leitor = new();
        ChavesNoEnvelope.Ler(leitor, new JsonObject { ["chave"] = texto }, "parte");

        leitor.Falhou.Should().BeTrue();
        leitor.Erro.Message.Should().Contain("chave");
    }

    [Fact]
    public void Ler_ChaveAusente_Recusa()
    {
        LeitorEnvelope leitor = new();

        ChavesNoEnvelope.Ler(leitor, new JsonObject(), "parte");

        leitor.Falhou.Should().BeTrue();
        leitor.Erro.Message.Should().Contain("chave");
    }

    [Fact]
    public void ExigirUnicas_ChaveRepetida_Recusa()
    {
        LeitorEnvelope leitor = new();
        ChaveLocal chave = ChaveLocal.Criar("fase_inscricao").Value;
        ChavesNoEnvelope.ExigirUnicas(leitor, [chave, chave], "fases");

        leitor.Falhou.Should().BeTrue();
        leitor.Erro.Message.Should().Contain("fase_inscricao");
    }
}
