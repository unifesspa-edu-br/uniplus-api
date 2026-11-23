namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

public sealed class TipoRenderizacaoNoEnvelopeTests
{
    private static JsonObject Payload(string tipo) => (JsonObject)JsonNode.Parse(
        $$"""{"dadosCitadosSemPergunta":[{"codigoDoDado":"FATO","rotulo":"Fato","tipoDeCampo":{{tipo}}}]}""")!;

    [Theory(DisplayName = "O envelope lê o código de cada tipo de campo da apresentação do fato")]
    [InlineData("NUMERO")]
    [InlineData("BOOLEANO")]
    [InlineData("SELECAO_UNICA")]
    [InlineData("SELECAO_MULTIPLA")]
    [InlineData("TEXTO")]
    [InlineData("DATA")]
    [InlineData("ENDERECO")]
    [InlineData("MUNICIPIO")]
    public void LerApresentacaoDosPressupostos_CodigoDeclarado_LeOTipo(string codigo)
    {
        LeitorEnvelope leitor = new();

        Dictionary<string, ApresentacaoDoFato> lidos = LeituraDoEdital.LerApresentacaoDosPressupostos(leitor, Payload($"\"{codigo}\""));

        leitor.Falhou.Should().BeFalse();
        lidos["FATO"].TipoRenderizacao.Should().Be(codigo);
    }

    [Theory(DisplayName = "O envelope recusa o tipo de campo que não é código declarado, listando os aceitos sem repetir o valor")]
    [InlineData("\"NENHUMA\"")]
    [InlineData("\"selecao_unica\"")]
    [InlineData("\"QUASE_TEXTO\"")]
    public void LerApresentacaoDosPressupostos_TipoForaDoCatalogo_RecusaOEnvelope(string tipo)
    {
        LeitorEnvelope leitor = new();

        LeituraDoEdital.LerApresentacaoDosPressupostos(leitor, Payload(tipo));

        leitor.Falhou.Should().BeTrue();
        leitor.Erro.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        leitor.Erro.Message.Should().Be(
            "Envelope malformado em 'dadosCitadosSemPergunta[0].tipoDeCampo': o tipo de campo não é um dos códigos aceitos: "
            + "NUMERO, BOOLEANO, SELECAO_UNICA, SELECAO_MULTIPLA, TEXTO, DATA, ENDERECO, MUNICIPIO.");
    }
}
