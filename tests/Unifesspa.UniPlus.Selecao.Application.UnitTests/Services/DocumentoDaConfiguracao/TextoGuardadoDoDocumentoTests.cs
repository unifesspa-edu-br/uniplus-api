namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.DocumentoDaConfiguracao;

using System.Text;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Kernel.Results;

using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

public sealed class TextoGuardadoDoDocumentoTests
{
    private static JsonObject ComProfundidade(int niveis)
    {
        JsonObject maisFundo = [];
        JsonObject raiz = maisFundo;
        for (int nivel = 1; nivel < niveis; nivel++)
        {
            raiz = new JsonObject { ["a"] = raiz };
        }

        return raiz;
    }

    [Fact(DisplayName = "O texto guardado mantém os caracteres não ASCII como estão, sem expandi-los em escapes")]
    public void NaoAscii_NaoExpande()
    {
        JsonObject documento = new() { ["nome"] = "Seleção ção é à" };

        Result<string> guardado = TextoGuardadoDoDocumento.Gerar(documento);

        guardado.Value.Should().Be("""{"nome":"Seleção ção é à"}""");
    }

    [Fact(DisplayName = "O texto guardado volta ao mesmo documento, inclusive com aspas, barras, quebras de linha e marcas de HTML")]
    public void TextoGuardado_VoltaAoMesmoDocumento()
    {
        JsonObject documento = new() { ["nome"] = "a \"b\" \\ c\n<tag>&'é" };

        Result<string> guardado = TextoGuardadoDoDocumento.Gerar(documento);

        JsonNode.DeepEquals(JsonNode.Parse(guardado.Value!), documento).Should().BeTrue();
    }

    [Fact(DisplayName = "O texto guardado no limite exato de bytes UTF-8 é aceito e um byte acima é recusado na raiz")]
    public void Tamanho_ELimitadoEmBytesUtf8()
    {
        string moldura = """{"a":""}""";
        int sobra = LimitesDoDocumento.TamanhoMaximoEmBytes - Encoding.UTF8.GetByteCount(moldura);

        Result<string> noLimite = TextoGuardadoDoDocumento.Gerar(new JsonObject { ["a"] = new string('é', sobra / 2) + new string('x', sobra % 2) });
        Result<string> acima = TextoGuardadoDoDocumento.Gerar(new JsonObject { ["a"] = new string('x', sobra + 1) });

        noLimite.IsSuccess.Should().BeTrue();
        acima.Errors.Select(static causa => (causa.Field, causa.Error.Code)).Should().Equal((string.Empty, DocumentoDaConfiguracaoErrorCodes.AcimaDoLimite));
    }

    [Fact(DisplayName = "A profundidade 32 é aceita e a 33 é recusada no objeto que passa do limite")]
    public void Profundidade_ELimitada()
    {
        string ponteiro = string.Concat(Enumerable.Repeat("/a", LimitesDoDocumento.ProfundidadeMaxima));

        TextoGuardadoDoDocumento.Gerar(ComProfundidade(LimitesDoDocumento.ProfundidadeMaxima)).IsSuccess.Should().BeTrue();
        TextoGuardadoDoDocumento.Gerar(ComProfundidade(LimitesDoDocumento.ProfundidadeMaxima + 1)).Errors
            .Select(static causa => (causa.Field, causa.Error.Code)).Should().Equal((ponteiro, DocumentoDaConfiguracaoErrorCodes.AcimaDoLimite));
    }

    [Fact(DisplayName = "Dez mil nós são aceitos e dez mil e um são recusados na raiz")]
    public void QuantidadeDeNos_ELimitada()
    {
        JsonObject NoLimite(int valores) => new() { ["a"] = new JsonArray([.. Enumerable.Range(0, valores - 2).Select(static numero => JsonValue.Create(numero))]) };

        TextoGuardadoDoDocumento.Gerar(NoLimite(LimitesDoDocumento.QuantidadeMaximaDeNos)).IsSuccess.Should().BeTrue();
        TextoGuardadoDoDocumento.Gerar(NoLimite(LimitesDoDocumento.QuantidadeMaximaDeNos + 1)).Errors
            .Select(static causa => (causa.Field, causa.Error.Code)).Should().Equal((string.Empty, DocumentoDaConfiguracaoErrorCodes.AcimaDoLimite));
    }

    [Fact(DisplayName = "O documento é obrigatório")]
    public void DocumentoNulo_Lanca() => Assert.Throws<ArgumentNullException>(() => TextoGuardadoDoDocumento.Gerar(null!));
}
