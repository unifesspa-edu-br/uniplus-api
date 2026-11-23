namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

public sealed class ProvaPresencialNoCertameTests
{
    [Fact]
    public void Projetar_ProvaPresencial_PublicaCamposSemExporInternos()
    {
        JsonObject envelope = ProjecaoDoCertameDeTeste.Envelope();
        envelope["provaPresencial"] = JsonNode.Parse("""{"temProvaPresencial":true,"municipios":[{"codigoIbge":"1504208","uf":"PA","nome":"Marabá","interno":"segredo"}]}""");
        envelope["motivosDeDecisao"] = new JsonArray("segredo");

        CertamePublicadoDto certame = ProjecaoDoCertameDeTeste.Projetado(envelope);
        string json = JsonSerializer.Serialize(certame, ProjecaoDoCertamePublicado.OpcoesDoDocumento);

        ClassificacaoDosBlocosDoCertame.Publicados.Should().Contain("provaPresencial");
        ClassificacaoDosBlocosDoCertame.Internos.Should().Contain("motivosDeDecisao");
        certame.ProvaPresencial.Municipios.Should().ContainSingle().Which.Should().Be(new MunicipioDeProvaCertameDto("1504208", "PA", "Marabá"));
        json.Should().NotContain("segredo");
        json.Should().NotContain("motivosDeDecisao");
    }

    [Fact]
    public void Projetar_SemProvaPresencial_Recusa()
    {
        JsonObject envelope = ProjecaoDoCertameDeTeste.Envelope();
        envelope.Remove("provaPresencial");

        ProjecaoDoCertameDeTeste.Projetar(envelope).IsFailure.Should().BeTrue();
    }
}
