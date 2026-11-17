namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.DocumentoDaConfiguracao;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

public sealed class PonteiroJsonTests
{
    [Theory(DisplayName = "O segmento escapa o til e a barra")]
    [InlineData("", "etapas", "/etapas")]
    [InlineData("/etapas", "0", "/etapas/0")]
    [InlineData("", "a/b", "/a~1b")]
    [InlineData("", "a~b", "/a~0b")]
    [InlineData("", "~1", "/~01")]
    public void Acrescentar_EscapaOSegmento(string ponteiro, string segmento, string esperado) =>
        PonteiroJson.Acrescentar(ponteiro, segmento).Should().Be(esperado);

    [Theory(DisplayName = "Os segmentos voltam ao nome original")]
    [InlineData("", new string[0])]
    [InlineData("/etapas/0/chave", new[] { "etapas", "0", "chave" })]
    [InlineData("/a~1b/~01", new[] { "a/b", "~1" })]
    public void Segmentos_DesfazOEscape(string ponteiro, string[] esperados) =>
        PonteiroJson.Segmentos(ponteiro).Should().Equal(esperados);

    [Fact(DisplayName = "O ponteiro resolve o valor no documento, ou nulo quando o documento não o tem")]
    public void Resolver_AchaOValorOuNulo()
    {
        JsonNode documento = JsonNode.Parse("""{ "etapas": [ { "nome": "Prova" } ], "a/b": 1 }""")!;

        PonteiroJson.Resolver(documento, "/etapas/0/nome")!.GetValue<string>().Should().Be("Prova");
        PonteiroJson.Resolver(documento, "/a~1b")!.GetValue<int>().Should().Be(1);
        PonteiroJson.Resolver(documento, string.Empty).Should().BeSameAs(documento);
        PonteiroJson.Resolver(documento, "/etapas/1/nome").Should().BeNull();
        PonteiroJson.Resolver(documento, "/etapas/x").Should().BeNull();
        PonteiroJson.Resolver(documento, "/etapas/0/nome/mais").Should().BeNull();
    }

    [Fact(DisplayName = "Os argumentos são obrigatórios")]
    public void ArgumentoNulo_Lanca()
    {
        Assert.Throws<ArgumentNullException>(() => PonteiroJson.Acrescentar(null!, "a"));
        Assert.Throws<ArgumentNullException>(() => PonteiroJson.Acrescentar(string.Empty, null!));
        Assert.Throws<ArgumentNullException>(() => PonteiroJson.Segmentos(null!));
        Assert.Throws<ArgumentNullException>(() => PonteiroJson.Resolver(null!, string.Empty));
    }
}
