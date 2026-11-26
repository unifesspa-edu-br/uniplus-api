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
    [InlineData("ExemploChaveInvalida")]
    [InlineData("etapa__x")]
    [InlineData("etapa-")]
    [InlineData("")]
    public void Ler_ChaveMalformada_Recusa(string texto)
    {
        LeitorEnvelope leitor = new();
        ChavesNoEnvelope.Ler(leitor, new JsonObject { ["chave"] = texto }, "parte");

        leitor.Falhou.Should().BeTrue();
        leitor.Erro.Message.Should().Contain("chave");
        if (!string.IsNullOrEmpty(texto))
        {
            leitor.Erro.Message.Should().NotContain(texto);
        }
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
        ChaveLocal chave = ChaveLocal.Criar("chave_exemplo_repetida").Value;
        ChavesNoEnvelope.ExigirUnicas(leitor, [chave, chave], "fases");

        leitor.Falhou.Should().BeTrue();
        leitor.Erro.Message.Should().Contain("aparece mais de uma vez").And.NotContain("chave_exemplo_repetida");
        leitor.Falha<object>().Errors.Should().ContainSingle().Which.Field.Should().BeNull();
    }
}
