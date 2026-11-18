namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.DocumentoDaConfiguracao;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Kernel.Results;

using NSubstitute;

using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

public sealed class SubstitutoIsoladoNoDocumentoTests
{
    private static (string? Ponteiro, string Codigo)[] Causas(string texto) =>
        [.. ConferenciaDaEstruturaDoDocumento.Conferir(texto).Errors.Select(static causa => (causa.Field, causa.Error.Code))];

    [Theory(DisplayName = "O valor de texto com metade de um par substituto é recusado como valor inválido, no ponteiro do valor")]
    [InlineData("""{ "a": "\uD800" }""", "/a")]
    [InlineData("""{ "a": "x\uDC00y" }""", "/a")]
    [InlineData("""{ "a": "\uD800A" }""", "/a")]
    [InlineData("""{ "a": "\uDE00\uD83D" }""", "/a")]
    [InlineData("""{ "x": { "y": "ok\uD800" } }""", "/x/y")]
    [InlineData("""{ "a/b~c": "\uD800" }""", "/a~1b~0c")]
    public void ValorComSubstitutoIsolado_ERecusadoNoPonteiroDoValor(string texto, string ponteiro) =>
        Causas(texto).Should().Equal((ponteiro, DocumentoDaConfiguracaoErrorCodes.ValorInvalido));

    [Theory(DisplayName = "O texto com metade de um par substituto numa lista é recusado no ponteiro do elemento")]
    [InlineData("""{ "a": [ "ok", "\uD800" ] }""", "/a/1")]
    [InlineData("""{ "etapas": [ { "nome": "Prova" }, { "nome": "Segunda\uDC00fase" } ] }""", "/etapas/1/nome")]
    public void ElementoDeListaComSubstitutoIsolado_ERecusadoNoPonteiroDoElemento(string texto, string ponteiro) =>
        Causas(texto).Should().Equal((ponteiro, DocumentoDaConfiguracaoErrorCodes.ValorInvalido));

    [Theory(DisplayName = "O nome de propriedade com metade de um par substituto é recusado como valor inválido, no ponteiro do objeto que o contém")]
    [InlineData("""{ "a\uD800b": 1 }""", "")]
    [InlineData("""{ "x": { "k\uDC00": [] } }""", "/x")]
    [InlineData("""{ "x": [ { "k": 1 }, { "\uD800": 2 } ] }""", "/x/1")]
    public void NomeComSubstitutoIsolado_ERecusadoNoPonteiroDoObjeto(string texto, string ponteiro) =>
        Causas(texto).Should().Equal((ponteiro, DocumentoDaConfiguracaoErrorCodes.ValorInvalido));

    [Theory(DisplayName = "O par substituto completo, escrito em escapes ou direto, é aceito no valor e no nome")]
    [InlineData("""{ "a": "😀" }""")]
    [InlineData("""{ "😀": 1 }""")]
    [InlineData("""{ "a": "Prova 😀", "😀": [ "😀" ] }""")]
    [InlineData("""{ "a": "x😀yA" }""")]
    public void ParSubstitutoCompleto_EAceito(string texto) =>
        ConferenciaDaEstruturaDoDocumento.Conferir(texto).IsSuccess.Should().BeTrue();

    [Fact(DisplayName = "As causas de metade de par substituto acumulam com o caractere nulo e com a propriedade repetida, na ordem do texto")]
    public void SubstitutosIsolados_AcumulamComAsOutrasCausas() =>
        Causas("""{ "a": "\uD800", "b": [ "\u0000", "\uDC00" ], "\uD800": 1, "c": 1, "c": 2 }""").Should().Equal(
            ("/a", DocumentoDaConfiguracaoErrorCodes.ValorInvalido),
            ("/b/0", DocumentoDaConfiguracaoErrorCodes.ValorInvalido),
            ("/b/1", DocumentoDaConfiguracaoErrorCodes.ValorInvalido),
            (string.Empty, DocumentoDaConfiguracaoErrorCodes.ValorInvalido),
            (string.Empty, DocumentoDaConfiguracaoErrorCodes.PropriedadeRepetida));

    [Fact(DisplayName = "Nomes distintos com metade de par substituto não passam por propriedade repetida")]
    public void NomesDistintosComSubstitutoIsolado_NaoSaoPropriedadeRepetida() =>
        Causas("""{ "\uD800": 1, "\uD801": 2 }""").Should().OnlyContain(static causa => causa.Codigo == DocumentoDaConfiguracaoErrorCodes.ValorInvalido);

    [Fact(DisplayName = "O valor dentro de uma propriedade de nome inválido ainda é conferido, com ponteiro só de caracteres ASCII")]
    public void ValorSobNomeComSubstitutoIsolado_EConferidoComPonteiroSemSubstituto()
    {
        (string? Ponteiro, string Codigo)[] causas = Causas("""{ "\uD800": "\u0000" }""");

        causas.Select(static causa => causa.Codigo).Should().Equal(DocumentoDaConfiguracaoErrorCodes.ValorInvalido, DocumentoDaConfiguracaoErrorCodes.ValorInvalido);
        causas.SelectMany(static causa => causa.Ponteiro!).Should().OnlyContain(static letra => !char.IsSurrogate(letra));
    }

    [Fact(DisplayName = "A mensagem não repete o texto digitado, nem o do valor nem o do nome")]
    public void Mensagem_NaoRepeteOTextoDigitado()
    {
        Result<JsonObject> resultado = ConferenciaDaEstruturaDoDocumento.Conferir("""{ "SEGREDO-1\uD800": "SEGREDO-2\uDC00" }""");

        resultado.Errors.Select(static causa => causa.Error.Message).Should().OnlyContain(static mensagem => !mensagem.Contains("SEGREDO", StringComparison.Ordinal));
        resultado.Errors.Select(static causa => causa.Field).Should().OnlyContain(static ponteiro => !ponteiro!.Contains("SEGREDO", StringComparison.Ordinal) || ponteiro.StartsWith("/SEGREDO-1", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "A verificação dos limites do texto guardado também não lança com o escape isolado")]
    public void ConferirLimites_NaoLancaComSubstitutoIsolado()
    {
        IReadOnlyList<FieldError> causas = ConferenciaDaEstruturaDoDocumento.ConferirLimites("""{ "a": "\uD800" }""");

        causas.Select(static causa => causa.Field).Should().Equal("/a");
    }

    [Fact(DisplayName = "A leitura recusa o documento com metade de par substituto sem normalizar nem avaliar")]
    public void Leitura_RecusaSemNormalizarNemAvaliar()
    {
        CenarioDoRascunho cenario = new();

        Result<DocumentoLido> resultado = new Unifesspa.UniPlus.Selecao.Application.Services.LeituraDoDocumentoDaConfiguracao(cenario.Esquema)
            .Ler("""{ "identificacao": { "nome": "Processo \uD800" }, "etapas": [ { "chave": "\uDC00" } ] }""");

        resultado.Errors.Select(static causa => causa.Field).Should().Equal("/identificacao/nome", "/etapas/0/chave");
        cenario.Esquema.DidNotReceiveWithAnyArgs().Avaliar(default!);
    }

    [Fact(DisplayName = "A preparação recusa o documento com metade de par substituto na forma, com o grupo da forma na frente")]
    public void Preparacao_RecusaNaForma()
    {
        Result<Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos.DocumentoPreparado> resultado =
            new CenarioDoRascunho().Preparacao.Preparar(CenarioDoRascunho.DocumentoValido(nome: "Processo\\uD800"));

        resultado.Errors.Select(static causa => (causa.Field, causa.Error.Code)).Should().Equal(
            (null, DocumentoDaConfiguracaoErrorCodes.FormaInvalida),
            ("/identificacao/nome", DocumentoDaConfiguracaoErrorCodes.ValorInvalido));
    }

    [Fact(DisplayName = "A preparação guarda o par substituto completo sem perda, como texto UTF-16 válido")]
    public void Preparacao_GuardaOParCompleto()
    {
        Result<Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos.DocumentoPreparado> resultado =
            new CenarioDoRascunho().Preparacao.Preparar(CenarioDoRascunho.DocumentoValido(nome: "Processo \\uD83D\\uDE00"));

        resultado.IsSuccess.Should().BeTrue();
        JsonNode.Parse(resultado.Value!.Documento.Conteudo)!["identificacao"]!["nome"]!.GetValue<string>().Should().Be("Processo \U0001F600");
    }
}
