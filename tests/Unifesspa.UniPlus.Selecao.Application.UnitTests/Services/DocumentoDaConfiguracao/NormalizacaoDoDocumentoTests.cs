namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.DocumentoDaConfiguracao;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Kernel.Results;

using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

public sealed class NormalizacaoDoDocumentoTests
{
    private static readonly NormalizacaoDoDocumento Normalizacao = new(FormaDeTeste.Documento());

    private static JsonObject Normalizar(string json, out IReadOnlyList<FieldError> repetidas)
    {
        JsonObject documento = JsonNode.Parse(json)!.AsObject();
        repetidas = Normalizacao.Normalizar(documento);
        return documento;
    }

    private static JsonObject Normalizar(string json) => Normalizar(json, out _);

    private static string?[] Chaves(JsonObject documento, string lista) =>
        [.. documento[lista]!.AsArray().Select(static parte => parte!["chave"]?.GetValue<string>())];

    [Fact(DisplayName = "Os campos só de leitura saem da raiz, de partes aninhadas e de objetos sem chave")]
    public void CamposSomenteLeitura_Saem()
    {
        JsonObject documento = Normalizar("""
            { "id": "0196b3c2", "nome": "N", "etapas": [ { "chave": "a", "descritivo": "D", "nome": "E" } ],
              "identificacao": { "nome": "P", "unidadeNome": "U" } }
            """);

        documento.ContainsKey("id").Should().BeFalse();
        documento["etapas"]![0]!.AsObject().ContainsKey("descritivo").Should().BeFalse();
        documento["identificacao"]!.AsObject().ContainsKey("unidadeNome").Should().BeFalse();
        documento["nome"]!.GetValue<string>().Should().Be("N");
        documento["etapas"]![0]!["nome"]!.GetValue<string>().Should().Be("E");
    }

    [Fact(DisplayName = "O que o esquema não conhece fica como veio, para a avaliação recusar")]
    public void CampoDesconhecido_FicaComoVeio()
    {
        JsonObject documento = Normalizar("""{ "invento": 1, "etapas": [ { "invento": 2 } ] }""");

        documento["invento"]!.GetValue<int>().Should().Be(1);
        documento["etapas"]![0]!["invento"]!.GetValue<int>().Should().Be(2);
    }

    [Fact(DisplayName = "A parte sem chave fica sem chave, em qualquer posição, para a avaliação do esquema recusar")]
    public void ParteSemChave_NaoRecebeChave()
    {
        JsonObject documento = Normalizar("""
            { "etapas": [ { "chave": "prova" }, { "itens": [ {} ], "bancas": [ {} ] } ], "cronograma": { "fases": [ {} ] } }
            """);

        Chaves(documento, "etapas").Should().Equal("prova", null);
        Chaves(documento["etapas"]![1]!.AsObject(), "itens").Should().Equal((string?)null);
        Chaves(documento["etapas"]![1]!.AsObject(), "bancas").Should().Equal((string?)null);
        Chaves(documento["cronograma"]!.AsObject(), "fases").Should().Equal((string?)null);
    }

    [Fact(DisplayName = "A chave escrita é conservada como veio, em qualquer posição da lista")]
    public void ChaveEscrita_EConservadaComoVeio()
    {
        const string Texto = """{ "etapas": [ { "chave": "segunda" }, { "chave": "primeira" }, { "chave": "etapa-1" } ] }""";

        Chaves(Normalizar(Texto), "etapas").Should().Equal("segunda", "primeira", "etapa-1");
    }

    [Fact(DisplayName = "Ausente não é vazio: chave vazia ou nula, lista vazia e texto vazio ficam como vieram")]
    public void Vazio_NaoEAusente()
    {
        JsonObject documento = Normalizar("""{ "nome": "", "etapas": [ { "chave": "" }, { "chave": null, "itens": [], "nome": "" } ] }""");

        documento["nome"]!.GetValue<string>().Should().BeEmpty();
        documento["etapas"]![0]!["chave"]!.GetValue<string>().Should().BeEmpty();
        documento["etapas"]![1]!.AsObject()["chave"].Should().BeNull();
        documento["etapas"]![1]!.AsObject().ContainsKey("chave").Should().BeTrue();
        documento["etapas"]![1]!["itens"]!.AsArray().Should().BeEmpty();
        documento["etapas"]![1]!["nome"]!.GetValue<string>().Should().BeEmpty();
    }

    [Fact(DisplayName = "Valor que não é objeto no lugar de uma parte fica como veio e não derruba a normalização")]
    public void ParteQueNaoEObjeto_FicaComoVeio()
    {
        JsonObject documento = Normalizar("""{ "etapas": [ "texto", 5, null, {} ] }""");

        documento["etapas"]!.AsArray().Select(static parte => parte?.ToJsonString()).Should().Equal("\"texto\"", "5", null, "{}");
    }

    [Fact(DisplayName = "A chave repetida é apontada nas duas partes, no ponteiro da chave")]
    public void ChaveRepetida_ApontaAsDuasPartes()
    {
        _ = Normalizar("""{ "etapas": [ { "chave": "a" }, { "chave": "b" }, { "chave": "a" } ] }""", out IReadOnlyList<FieldError> repetidas);

        repetidas.Select(static causa => causa.Field).Should().Equal("/etapas/0/chave", "/etapas/2/chave");
        repetidas.Should().OnlyContain(static causa => causa.Error.Code == DocumentoDaConfiguracaoErrorCodes.ChaveRepetida);
    }

    [Fact(DisplayName = "A mesma chave em tipos diferentes, ou no item de etapas diferentes, não se repete")]
    public void MesmaChaveEmOutroConjunto_NaoSeRepete()
    {
        _ = Normalizar(
            """
            { "etapas": [ { "chave": "a", "itens": [ { "chave": "q" } ] }, { "chave": "b", "itens": [ { "chave": "q" } ] } ],
              "cronograma": { "fases": [ { "chave": "a" } ] } }
            """,
            out IReadOnlyList<FieldError> repetidas);

        repetidas.Should().BeEmpty();
    }

    [Fact(DisplayName = "O item repetido dentro da mesma etapa é apontado")]
    public void ItemRepetidoNaMesmaEtapa_EApontado()
    {
        _ = Normalizar("""{ "etapas": [ { "itens": [ { "chave": "q" }, { "chave": "q" } ] } ] }""", out IReadOnlyList<FieldError> repetidas);

        repetidas.Select(static causa => causa.Field).Should().Equal("/etapas/0/itens/0/chave", "/etapas/0/itens/1/chave");
    }

    [Fact(DisplayName = "A chave que não é texto não entra na conferência de repetição")]
    public void ChaveQueNaoETexto_NaoEConferida()
    {
        _ = Normalizar("""{ "etapas": [ { "chave": 1 }, { "chave": 1 } ] }""", out IReadOnlyList<FieldError> repetidas);

        repetidas.Should().BeEmpty();
    }

    [Fact(DisplayName = "A mensagem da chave repetida não repete o texto digitado")]
    public void ChaveRepetida_MensagemNaoRepeteATexto()
    {
        _ = Normalizar("""{ "etapas": [ { "chave": "SEGREDO-1" }, { "chave": "SEGREDO-1" } ] }""", out IReadOnlyList<FieldError> repetidas);

        repetidas.Select(static causa => causa.Error.Message).Should().NotContain(static mensagem => mensagem.Contains("SEGREDO", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "O que está dentro de um campo só de leitura não conta para a repetição")]
    public void DentroDeCampoSomenteLeitura_NaoConta()
    {
        FormaDoNo forma = FormaDeTeste.Objeto(new()
        {
            ["copia"] = new FormaDoNo(
                somenteLeitura: true,
                parte: null,
                () => new Dictionary<string, FormaDoNo>(),
                () => FormaDeTeste.Objeto(new() { ["chave"] = FormaDeTeste.Valor() }, new ParteComChave("etapa", EscopoDaChave.Documento))),
            ["etapas"] = FormaDeTeste.Lista(FormaDeTeste.Objeto(new() { ["chave"] = FormaDeTeste.Valor() }, new ParteComChave("etapa", EscopoDaChave.Documento))),
        });
        JsonObject documento = JsonNode.Parse("""{ "copia": [ { "chave": "a" } ], "etapas": [ { "chave": "a" } ] }""")!.AsObject();

        new NormalizacaoDoDocumento(forma).Normalizar(documento).Should().BeEmpty();
        documento.ContainsKey("copia").Should().BeFalse();
    }

    [Fact(DisplayName = "O documento é obrigatório")]
    public void DocumentoNulo_Lanca() => Assert.Throws<ArgumentNullException>(() => Normalizacao.Normalizar(null!));
}
