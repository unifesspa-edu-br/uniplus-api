namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

public sealed class ChavesDasBancasNoEnvelopeTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("Banca")]
    [InlineData("banca__repetida")]
    public void LerBanca_ChaveAusenteOuMalformada_Recusa(string? chave)
    {
        LeitorEnvelope leitor = new();
        JsonObject banca = chave is null ? [] : new() { ["chave"] = chave };

        BancaLida.Ler(leitor, banca, "banca");

        leitor.Falhou.Should().BeTrue();
        leitor.Erro.Message.Should().Contain("chave");
    }
}
