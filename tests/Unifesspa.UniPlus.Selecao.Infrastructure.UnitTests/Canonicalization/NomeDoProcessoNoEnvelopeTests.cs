namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

public sealed class NomeDoProcessoNoEnvelopeTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void LerNome_AusenteOuVazio_Recusa(string? nome)
    {
        JsonObject documento = [];
        if (nome is not null)
        {
            documento["nome"] = nome;
        }
        LeitorEnvelope leitor = new();
        LeituraDoEdital.LerNome(leitor, documento);
        leitor.Falhou.Should().BeTrue();
    }

    [Theory]
    [InlineData(300, false)]
    [InlineData(301, true)]
    public void LerNome_ComprimentoNaBorda_ConfereLimite(int comprimento, bool recusado)
    {
        LeitorEnvelope leitor = new();
        LeituraDoEdital.LerNome(leitor, new JsonObject { ["nome"] = new string('a', comprimento) });
        leitor.Falhou.Should().Be(recusado);
    }
}
