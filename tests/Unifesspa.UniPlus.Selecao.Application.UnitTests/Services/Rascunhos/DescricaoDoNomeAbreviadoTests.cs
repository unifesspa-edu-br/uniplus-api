namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.Rascunhos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;

/// <summary>A regra que abrevia o nome sai na divulgação só quando o nome abreviado é um dos campos públicos.</summary>
public sealed class DescricaoDoNomeAbreviadoTests
{
    private static JsonObject Descrito(string texto)
    {
        JsonObject documento = JsonNode.Parse(texto)!.AsObject();
        DescricaoDoNomeAbreviado.Descrever(documento);
        return documento;
    }

    [Fact(DisplayName = "O nome abreviado entre os campos públicos traz a regra vigente logo depois dos campos")]
    public void Descrever_NomeAbreviadoDivulgado_EscreveARegraVigente()
    {
        JsonObject documento = Descrito("""{ "divulgacao": { "camposPublicos": [ "NUMERO_INSCRICAO", "NOME_ABREVIADO" ], "justificativa": "Texto de exemplo" } }""");

        JsonObject divulgacao = documento["divulgacao"]!.AsObject();
        divulgacao["regraNomeAbreviado"]!.GetValue<string>().Should().Be(RegrasDeNomeAbreviado.Vigente);
        divulgacao.Select(static propriedade => propriedade.Key).Should().Equal("camposPublicos", "regraNomeAbreviado", "justificativa");
    }

    [Theory(DisplayName = "Sem o nome abreviado entre os campos públicos, nenhuma regra é escrita")]
    [InlineData("""{ "divulgacao": { "camposPublicos": [ "NUMERO_INSCRICAO", "NOME" ] } }""")]
    [InlineData("""{ "divulgacao": { "camposPublicos": [] } }""")]
    [InlineData("""{ "divulgacao": { "camposPublicos": [ 1, null ] } }""")]
    [InlineData("""{ "divulgacao": { } }""")]
    [InlineData("""{ "identificacao": { } }""")]
    public void Descrever_SemNomeAbreviadoDivulgado_NaoEscreveARegra(string texto)
    {
        JsonObject documento = Descrito(texto);

        documento["divulgacao"]?["regraNomeAbreviado"].Should().BeNull();
    }
}
