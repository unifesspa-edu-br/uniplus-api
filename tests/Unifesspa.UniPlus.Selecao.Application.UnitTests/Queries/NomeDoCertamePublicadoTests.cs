namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

public sealed class NomeDoCertamePublicadoTests
{
    [Fact]
    public void Projetar_NomeNoDocumento_UsaNomeCongelado()
    {
        JsonObject documento = ProjecaoDoCertameDeTeste.Envelope();
        documento["nome"] = "Seleção publicada";
        ProjecaoDoCertameDeTeste.Projetado(documento).Nome.Should().Be("Seleção publicada");
    }

    [Fact]
    public void Projetar_NomeAusente_Recusa()
    {
        JsonObject documento = ProjecaoDoCertameDeTeste.Envelope();
        documento.Remove("nome");
        ProjecaoDoCertameDeTeste.Projetar(documento).IsFailure.Should().BeTrue();
    }
}
