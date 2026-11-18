namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Contratos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using static Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Contratos.ContratoDaConfiguracao;

/// <summary>
/// O procedimento de recurso de cada matéria, declarado na janela de recurso da fase e da etapa: o tipo de decisão e a
/// regra do catálogo são obrigatórios, e o alvo, quando existe, é um item de avaliação pela chave local ou uma área do
/// ENEM pelo código, nunca os dois.
/// </summary>
public sealed class ProcedimentosDeRecursoDoEsquemaDaConfiguracaoTests
{
    private const string Comuns = "comuns.schema.json";

    private const string Item = """
        { "tipoDeDecisao": "CLASSIFICACAO", "alvo": { "areaDoEnem": "REDACAO" },
          "regra": { "codigo": "PROCEDIMENTO-RECURSO-RECALCULO", "versao": "v1" } }
        """;

    private static JsonNode Procedimento(string json = Item) => Documento(json);

    private static string[] ErrosDaEtapa(JsonNode procedimento)
    {
        JsonNode documento = Carregar(Path.Join("exemplos", "inscricao-pelo-sistema.json"));
        documento["etapas"]![0]!["recursos"]![0]!["procedimentosDeRecurso"] = new JsonArray(procedimento.DeepClone());
        return CaminhosComErro(documento);
    }

    private static string[] ErrosDaFase(JsonNode procedimento)
    {
        JsonNode documento = Carregar(Path.Join("exemplos", "inscricao-pelo-sistema.json"));
        JsonNode fase = documento["cronograma"]!["fases"]![0]!;
        fase["regraRecurso"] = Documento("""{ "regraCodigo": "RECURSO-PRAZO", "regraVersao": "v1" }""");
        fase["regraRecurso"]!["procedimentosDeRecurso"] = new JsonArray(procedimento.DeepClone());
        return CaminhosComErro(documento);
    }

    [Fact(DisplayName = "O procedimento com alvo por área do ENEM, na etapa e na fase, valida")]
    public void Procedimento_ComAlvoPorArea_Valida()
    {
        ErrosDaEtapa(Procedimento()).Should().BeEmpty();
        ErrosDaFase(Procedimento()).Should().BeEmpty();
    }

    [Fact(DisplayName = "O procedimento sem alvo, para o tipo de decisão inteiro, valida")]
    public void Procedimento_SemAlvo_Valida()
    {
        JsonNode procedimento = Procedimento();
        procedimento.AsObject().Remove("alvo");

        ErrosDaEtapa(procedimento).Should().BeEmpty();
    }

    [Fact(DisplayName = "O alvo por item de avaliação cita a chave local do item")]
    public void Procedimento_ComAlvoPorItem_CitaAChaveLocal()
    {
        JsonNode procedimento = Procedimento();
        procedimento["alvo"] = Documento("""{ "itemDeAvaliacaoChave": "redacao" }""");
        ErrosDaEtapa(procedimento).Should().BeEmpty();

        procedimento["alvo"] = Documento("""{ "itemDeAvaliacaoChave": "Item Redação" }""");
        ErrosDaEtapa(procedimento).Should().NotBeEmpty();
    }

    [Theory(DisplayName = "O procedimento sem o tipo de decisão ou sem a regra é recusado")]
    [InlineData("tipoDeDecisao")]
    [InlineData("regra")]
    public void Procedimento_SemCampoObrigatorio_Recusa(string campo)
    {
        JsonNode procedimento = Procedimento();
        procedimento.AsObject().Remove(campo);

        ErrosDaEtapa(procedimento).Should().NotBeEmpty();
    }

    [Fact(DisplayName = "O alvo com o item de avaliação e a área do ENEM ao mesmo tempo é recusado")]
    public void Alvo_ComOsDois_Recusa()
    {
        JsonNode procedimento = Procedimento();
        procedimento["alvo"] = Documento("""{ "itemDeAvaliacaoChave": "redacao", "areaDoEnem": "REDACAO" }""");

        ErrosDaEtapa(procedimento).Should().NotBeEmpty();
    }

    [Fact(DisplayName = "O alvo vazio é recusado")]
    public void Alvo_Vazio_Recusa()
    {
        JsonNode procedimento = Procedimento();
        procedimento["alvo"] = Documento("{}");

        ErrosDaEtapa(procedimento).Should().NotBeEmpty();
    }

    [Theory(DisplayName = "O valor fora do vocabulário é recusado")]
    [InlineData("tipoDeDecisao", "RECURSO_QUALQUER")]
    public void Procedimento_ValorForaDoVocabulario_Recusa(string campo, string valor)
    {
        JsonNode procedimento = Procedimento();
        procedimento[campo] = valor;

        ErrosDaEtapa(procedimento).Should().NotBeEmpty();
    }

    [Fact(DisplayName = "A área do ENEM e a regra fora do rol de procedimentos são recusadas")]
    public void Procedimento_AreaOuRegraDesconhecida_Recusa()
    {
        JsonNode area = Procedimento();
        area["alvo"] = Documento("""{ "areaDoEnem": "GEOGRAFIA" }""");
        ErrosDaEtapa(area).Should().NotBeEmpty();

        JsonNode regra = Procedimento();
        regra["regra"]!["codigo"] = "RECURSO-PRAZO";
        ErrosDaEtapa(regra).Should().NotBeEmpty();
    }

    [Fact(DisplayName = "O mesmo procedimento repetido na lista é recusado")]
    public void Procedimentos_Repetidos_Recusa()
    {
        JsonNode documento = Carregar(Path.Join("exemplos", "inscricao-pelo-sistema.json"));
        documento["etapas"]![0]!["recursos"]![0]!["procedimentosDeRecurso"] = new JsonArray(Procedimento(), Procedimento());

        CaminhosComErro(documento).Should().NotBeEmpty();
    }

    [Fact(DisplayName = "O procedimento não admite propriedade fora do esquema")]
    public void Procedimento_ComPropriedadeDesconhecida_Recusa()
    {
        JsonNode procedimento = Procedimento();
        procedimento["prazo"] = 3;

        ErrosDaEtapa(procedimento).Should().NotBeEmpty();
    }

    [Fact(DisplayName = "O esquema declara a lista de procedimentos no recurso comum à fase e à etapa")]
    public void Recurso_DeclaraOsProcedimentos() =>
        Esquema(Comuns)["$defs"]!["propriedadesDoRecurso"]!["properties"]!.AsObject().Should().ContainKey("procedimentosDeRecurso");
}
