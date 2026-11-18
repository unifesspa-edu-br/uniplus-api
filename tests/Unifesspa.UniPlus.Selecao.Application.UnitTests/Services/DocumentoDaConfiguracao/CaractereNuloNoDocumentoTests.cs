namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.DocumentoDaConfiguracao;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Kernel.Results;

using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

public sealed class CaractereNuloNoDocumentoTests
{
    private static (string? Ponteiro, string Codigo)[] Causas(string texto) =>
        [.. ConferenciaDaEstruturaDoDocumento.Conferir(texto).Errors.Select(static causa => (causa.Field, causa.Error.Code))];

    [Theory(DisplayName = "O valor de texto com o caractere nulo é recusado como valor inválido, no ponteiro do valor")]
    [InlineData("""{ "a": "x\u0000y" }""", "/a")]
    [InlineData("""{ "a": "\u0000" }""", "/a")]
    [InlineData("""{ "x": { "y": "ok\u0000" } }""", "/x/y")]
    [InlineData("""{ "a": [ "ok", "\u0000" ] }""", "/a/1")]
    [InlineData("""{ "etapas": [ { "nome": "Prova" }, { "nome": "Segunda\u0000fase" } ] }""", "/etapas/1/nome")]
    [InlineData("""{ "a/b~c": "\u0000" }""", "/a~1b~0c")]
    public void ValorComCaractereNulo_ERecusadoNoPonteiroDoValor(string texto, string ponteiro) =>
        Causas(texto).Should().Equal((ponteiro, DocumentoDaConfiguracaoErrorCodes.ValorInvalido));

    [Theory(DisplayName = "O nome de propriedade com o caractere nulo é recusado como valor inválido, no ponteiro do objeto que o contém")]
    [InlineData("""{ "a\u0000b": 1 }""", "")]
    [InlineData("""{ "x": { "k\u0000": [] } }""", "/x")]
    [InlineData("""{ "x": [ { "k": 1 }, { "\u0000": 2 } ] }""", "/x/1")]
    public void NomeComCaractereNulo_ERecusadoNoPonteiroDoObjeto(string texto, string ponteiro) =>
        Causas(texto).Should().Equal((ponteiro, DocumentoDaConfiguracaoErrorCodes.ValorInvalido));

    [Fact(DisplayName = "O nome e o valor com o caractere nulo no mesmo item dão uma causa para cada")]
    public void NomeEValorComCaractereNulo_DaoUmaCausaParaCada() =>
        Causas("""{ "a\u0000": "b\u0000" }""").Should().Equal(
            (string.Empty, DocumentoDaConfiguracaoErrorCodes.ValorInvalido),
            ("/a\0", DocumentoDaConfiguracaoErrorCodes.ValorInvalido));

    [Fact(DisplayName = "Todos os caracteres nulos são apontados e acumulam com a propriedade repetida")]
    public void CaracteresNulos_AcumulamComAsOutrasCausas() =>
        Causas("""{ "a": "\u0000", "b": [ "\u0000" ], "c": 1, "c": 2 }""").Should().Equal(
            ("/a", DocumentoDaConfiguracaoErrorCodes.ValorInvalido),
            ("/b/0", DocumentoDaConfiguracaoErrorCodes.ValorInvalido),
            (string.Empty, DocumentoDaConfiguracaoErrorCodes.PropriedadeRepetida));

    [Theory(DisplayName = "O que se parece com o caractere nulo mas não é o caractere nulo é aceito")]
    [InlineData("""{ "a": "\\u0000" }""")]
    [InlineData("""{ "a": "\u0041\u00e9\u0001" }""")]
    [InlineData("""{ "\\u0000": 1 }""")]
    [InlineData("""{ "a": "texto sem escape" }""")]
    public void SemCaractereNulo_EAceito(string texto) =>
        ConferenciaDaEstruturaDoDocumento.Conferir(texto).IsSuccess.Should().BeTrue();

    [Fact(DisplayName = "A mensagem não repete o texto digitado, nem o do valor nem o do nome")]
    public void Mensagem_NaoRepeteOTextoDigitado()
    {
        Result<JsonObject> resultado = ConferenciaDaEstruturaDoDocumento.Conferir("""{ "SEGREDO-1\u0000": "SEGREDO-2\u0000" }""");

        resultado.Errors.Select(static causa => causa.Error.Message).Should().OnlyContain(static mensagem => !mensagem.Contains("SEGREDO", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "O limite recusado antes interrompe a varredura sem as causas de caractere nulo")]
    public void LimiteDeProfundidade_InterrompeAVarredura()
    {
        string texto = "{\"n\":\"\\u0000\"," + string.Concat(Enumerable.Repeat("\"a\":{", LimitesDoDocumento.ProfundidadeMaxima)) + new string('}', LimitesDoDocumento.ProfundidadeMaxima) + "}";

        Causas(texto).Should().ContainSingle().Which.Codigo.Should().Be(DocumentoDaConfiguracaoErrorCodes.AcimaDoLimite);
    }
}
