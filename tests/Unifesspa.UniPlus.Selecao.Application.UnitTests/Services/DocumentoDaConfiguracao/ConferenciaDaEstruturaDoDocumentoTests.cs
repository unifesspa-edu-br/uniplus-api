namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.DocumentoDaConfiguracao;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Kernel.Results;

using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

public sealed class ConferenciaDaEstruturaDoDocumentoTests
{
    private static (string? Ponteiro, string Codigo)[] Causas(string texto) =>
        [.. ConferenciaDaEstruturaDoDocumento.Conferir(texto).Errors.Select(static causa => (causa.Field, causa.Error.Code))];

    private static string ComTamanhoEmBytes(int bytes) => $$"""{"a":"{{new string('x', bytes - 8)}}"}""";

    // Objetos aninhados em uma única propriedade: o de índice 1 é a raiz, e o último é o mais fundo.
    private static string ComProfundidade(int niveis) => string.Concat(Enumerable.Repeat("{\"a\":", niveis - 1)) + "{}" + new string('}', niveis - 1);

    private static string ComValores(int valores) => $$"""{"a":[{{string.Join(',', Enumerable.Repeat("1", valores - 2))}}]}""";

    [Fact(DisplayName = "O documento de exatamente 2 MiB é aceito")]
    public void DoisMebibytesExatos_SaoAceitos()
    {
        string texto = ComTamanhoEmBytes(LimitesDoDocumento.TamanhoMaximoEmBytes);

        Encoding.UTF8.GetByteCount(texto).Should().Be(LimitesDoDocumento.TamanhoMaximoEmBytes);
        ConferenciaDaEstruturaDoDocumento.Conferir(texto).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Um byte acima de 2 MiB é recusado na raiz, sem apontar item")]
    public void UmByteAcima_ERecusadoNaRaiz() =>
        Causas(ComTamanhoEmBytes(LimitesDoDocumento.TamanhoMaximoEmBytes + 1)).Should().Equal((null, DocumentoDaConfiguracaoErrorCodes.AcimaDoLimite));

    [Fact(DisplayName = "O tamanho é medido em bytes UTF-8, não em caracteres")]
    public void Tamanho_EMedidoEmBytes()
    {
        string texto = $$"""{"a":"{{new string('é', LimitesDoDocumento.TamanhoMaximoEmBytes / 2)}}"}""";

        texto.Length.Should().BeLessThan(LimitesDoDocumento.TamanhoMaximoEmBytes);
        Causas(texto).Should().Equal((null, DocumentoDaConfiguracaoErrorCodes.AcimaDoLimite));
    }

    [Fact(DisplayName = "A profundidade 32 é aceita")]
    public void Profundidade32_EAceita() =>
        ConferenciaDaEstruturaDoDocumento.Conferir(ComProfundidade(LimitesDoDocumento.ProfundidadeMaxima)).IsSuccess.Should().BeTrue();

    [Fact(DisplayName = "A profundidade 33 é recusada no objeto que passa do limite")]
    public void Profundidade33_ERecusadaNoObjetoQuePassa()
    {
        string ponteiro = string.Concat(Enumerable.Repeat("/a", LimitesDoDocumento.ProfundidadeMaxima));

        Causas(ComProfundidade(LimitesDoDocumento.ProfundidadeMaxima + 1)).Should().Equal((ponteiro, DocumentoDaConfiguracaoErrorCodes.AcimaDoLimite));
    }

    [Fact(DisplayName = "Dez mil nós são aceitos")]
    public void DezMilNos_SaoAceitos() =>
        ConferenciaDaEstruturaDoDocumento.Conferir(ComValores(LimitesDoDocumento.QuantidadeMaximaDeNos)).IsSuccess.Should().BeTrue();

    [Fact(DisplayName = "Dez mil e um nós são recusados na raiz")]
    public void DezMilEUmNos_SaoRecusados() =>
        Causas(ComValores(LimitesDoDocumento.QuantidadeMaximaDeNos + 1)).Should().Equal((string.Empty, DocumentoDaConfiguracaoErrorCodes.AcimaDoLimite));

    [Theory(DisplayName = "O texto que não é JSON válido é recusado como forma inválida")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{")]
    [InlineData("{\"a\":}")]
    [InlineData("{\"a\":1,}")]
    [InlineData("{} texto depois")]
    [InlineData("{}{}")]
    [InlineData("// comentário\n{}")]
    [InlineData("{\"a\":\"\\u12\"}")]
    public void Malformado_EFormaInvalida(string texto) =>
        Causas(texto).Should().Equal((null, DocumentoDaConfiguracaoErrorCodes.FormaInvalida));

    [Theory(DisplayName = "A raiz que não é objeto é recusada como tipo inválido")]
    [InlineData("[]")]
    [InlineData("[{}]")]
    [InlineData("42")]
    [InlineData("\"texto\"")]
    [InlineData("null")]
    [InlineData("true")]
    public void RaizNaoObjeto_ETipoInvalido(string texto) =>
        Causas(texto).Should().Equal((string.Empty, DocumentoDaConfiguracaoErrorCodes.TipoInvalido));

    [Fact(DisplayName = "A propriedade repetida na raiz é recusada no ponteiro do objeto")]
    public void PropriedadeRepetidaNaRaiz_ApontaARaiz() =>
        Causas("""{ "a": 1, "a": 2 }""").Should().Equal((string.Empty, DocumentoDaConfiguracaoErrorCodes.PropriedadeRepetida));

    [Fact(DisplayName = "A propriedade repetida num objeto de uma lista é recusada no ponteiro desse objeto")]
    public void PropriedadeRepetidaEmLista_ApontaOObjeto() =>
        Causas("""{ "etapas": [ {}, { "nome": 1, "nome": 2 } ] }""").Should().Equal(("/etapas/1", DocumentoDaConfiguracaoErrorCodes.PropriedadeRepetida));

    [Fact(DisplayName = "As repetições saem na ordem do documento, o objeto que contém antes do contido, uma por objeto")]
    public void Repeticoes_SaemNaOrdemDoDocumento() =>
        Causas("""{ "x": { "a": 1, "a": 2, "a": 3 }, "y": 1, "y": 2, "z": [ { "k": 1, "k": 2 } ] }""").Select(static causa => causa.Ponteiro).Should().Equal(string.Empty, "/x", "/z/0");

    [Fact(DisplayName = "O nome escrito com escape é o mesmo nome: repete")]
    public void NomeComEscape_RepeteONomeSemEscape() =>
        Causas("""{ "a": 1, "\u0061": 2 }""").Should().Equal((string.Empty, DocumentoDaConfiguracaoErrorCodes.PropriedadeRepetida));

    [Fact(DisplayName = "O ponteiro escapa a barra e o til do nome da propriedade")]
    public void PonteiroDaRepeticao_EscapaBarraETil() =>
        Causas("""{ "a/b~c": { "k": 1, "k": 2 } }""").Should().Equal(("/a~1b~0c", DocumentoDaConfiguracaoErrorCodes.PropriedadeRepetida));

    [Fact(DisplayName = "A mensagem da propriedade repetida não repete o texto digitado")]
    public void PropriedadeRepetida_MensagemNaoRepeteOTextoDigitado()
    {
        Result<JsonObject> resultado = ConferenciaDaEstruturaDoDocumento.Conferir("""{ "SEGREDO-1": "SEGREDO-2", "SEGREDO-1": 2 }""");

        resultado.Errors.Select(static causa => causa.Error.Message).Should().NotContain(static mensagem => mensagem.Contains("SEGREDO", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "O documento válido devolve a árvore")]
    public void DocumentoValido_DevolveAArvore()
    {
        Result<JsonObject> resultado = ConferenciaDaEstruturaDoDocumento.Conferir("""{ "etapas": [ { "nome": "Prova" } ] }""");

        resultado.Value!["etapas"]![0]!["nome"]!.GetValue<string>().Should().Be("Prova");
    }

    [Fact(DisplayName = "A leitura do .NET 10 recusa a propriedade repetida quando configurada, mas a exceção não diz em que objeto")]
    public void ApiDoDotNet_RecusaARepeticaoSemDizerOndeEla_Esta()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("""{ "x": { "a": 1, "a": 2 } }""");

        Action recusa = () => JsonNode.Parse(utf8, nodeOptions: null, new JsonDocumentOptions { AllowDuplicateProperties = false });

        recusa.Should().Throw<JsonException>().Which.Path.Should().BeNull();
    }

    [Fact(DisplayName = "O texto é obrigatório")]
    public void TextoNulo_Lanca() => Assert.Throws<ArgumentNullException>(() => ConferenciaDaEstruturaDoDocumento.Conferir(null!));
}
