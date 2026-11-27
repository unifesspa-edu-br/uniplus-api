namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Contratos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using static Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Contratos.ContratoDaConfiguracao;

/// <summary>
/// A distribuição de vagas, as opções de curso e a cascata de remanejamento no esquema da configuração: a oferta e as
/// modalidades entram pelo código do cadastro, o quadro de vagas vai só como leitura e a quantidade que o edital fixa
/// é o único dado de quantidade editável.
/// </summary>
public sealed class VagasDoEsquemaDaConfiguracaoTests
{
    private const string Distribuicao = "distribuicao-de-vagas.schema.json";

    private const string Cascata = "ordem-de-remanejamento.schema.json";

    private const string DistribuicaoValida = """
        {
          "ofertaCursoCodigo": "ENG_CIVIL_MARABA", "vagasOfertadasBase": 40, "percentualDaReservaLegal": 0.5,
          "regraDistribuicao": { "codigo": "DISTRIB-VAGAS-LEI-12711", "versao": "v1" },
          "regraAjuste": null, "ajusteDoQuadro": null, "referenciaDemograficaCodigo": "CENSO_2022",
          "modalidades": [ { "codigo": "AC" }, { "codigo": "LB_PPI", "quantidadeDeclarada": null } ],
          "cursoNome": "Engenharia Civil", "cursoGrau": "Bacharelado", "unidadeOfertanteNome": "Faculdade de Engenharia", "turnos": [ "INTEGRAL" ],
          "quadro": [ { "modalidadeCodigo": "AC", "quantidade": 20 }, { "modalidadeCodigo": "LB_PPI", "quantidade": 20 } ],
          "totalDeVagasDoEdital": 40
        }
        """;

    private static JsonNode Alterada(string propriedade, JsonNode? valor)
    {
        JsonNode distribuicao = Documento(DistribuicaoValida);
        distribuicao[propriedade] = valor;
        return distribuicao;
    }

    [Fact(DisplayName = "A distribuição completa, com o quadro de leitura, valida")]
    public void Distribuicao_Completa_Valida() =>
        CaminhosComErro(Documento(DistribuicaoValida), Distribuicao).Should().BeEmpty();

    [Fact(DisplayName = "As vagas, as opções de curso e a cascata ficam no documento")]
    public void Documento_ComVagas_Valida() =>
        CaminhosComErro(Documento("""
            { "formato": "uniplus.selecao.configuracao", "versaoFormato": 1,
              "opcoesDeCurso": { "numero": 2 },
              "distribuicaoVagas": [ { "ofertaCursoCodigo": "ENG_CIVIL_MARABA" } ],
              "ordemDeRemanejamento": { "regra": { "codigo": "REMANEJAMENTO_ORDEM_LEI_12711", "versao": "v1" }, "destinoFinalCodigo": "AC",
                "destinos": [ { "modalidadeOrigemCodigo": "LB_PPI", "modalidadeDestinoCodigo": "LB_Q" } ] } }
            """)).Should().BeEmpty();

    [Theory(DisplayName = "A oferta fora do formato do código ou pelo identificador interno é recusada")]
    [InlineData("eng civil")]
    [InlineData("1ENG")]
    [InlineData("3f2504e0-4f89-11d3-9a0c-0305e82c3301")]
    public void Distribuicao_OfertaForaDoFormato_Recusa(string oferta) =>
        CaminhosComErro(Alterada("ofertaCursoCodigo", oferta), Distribuicao).Should().Contain("/ofertaCursoCodigo");

    [Theory(DisplayName = "O valor numérico fora do limite é recusado")]
    [InlineData("vagasOfertadasBase", 0)]
    [InlineData("percentualDaReservaLegal", 0.49)]
    [InlineData("percentualDaReservaLegal", 1.01)]
    [InlineData("percentualDaReservaLegal", 0.50001)]
    public void Distribuicao_NumeroForaDoLimite_Recusa(string propriedade, double valor)
    {
        ArgumentNullException.ThrowIfNull(propriedade);

        CaminhosComErro(Alterada(propriedade, valor), Distribuicao).Should().Contain("/" + propriedade);
    }

    [Fact(DisplayName = "A modalidade em minúsculas ou com a natureza copiada do cadastro é recusada; a descrição nula valida")]
    public void Modalidade_Invalida_Recusa()
    {
        JsonNode semDescricao = Alterada("modalidades", Documento("""[ { "codigo": "AC", "descricao": null } ]"""));
        JsonNode minuscula = Alterada("modalidades", Documento("""[ { "codigo": "ac" } ]"""));
        JsonNode natureza = Alterada("modalidades", Documento("""[ { "codigo": "AC", "naturezaLegal": "AMPLA" } ]"""));

        CaminhosComErro(semDescricao, Distribuicao).Should().BeEmpty();
        CaminhosComErro(minuscula, Distribuicao).Should().Contain("/modalidades/0/codigo");
        CaminhosComErro(natureza, Distribuicao).Should().Contain("/modalidades/0/naturezaLegal");
    }

    [Theory(DisplayName = "O ajuste do quadro exige a propriedade do tipo e recusa a do outro")]
    [InlineData("""{ "$tipo": "reduzirDe", "modalidadeCodigo": "AC_PCD" }""", true)]
    [InlineData("""{ "$tipo": "reduzirProporcionalEm", "modalidadeCodigos": [ "AC", "LB_Q" ] }""", true)]
    [InlineData("""{ "$tipo": "reduzirDe" }""", false)]
    [InlineData("""{ "$tipo": "reduzirDe", "modalidadeCodigo": "AC", "modalidadeCodigos": [ "AC" ] }""", false)]
    [InlineData("""{ "$tipo": "reduzirProporcionalEm", "modalidadeCodigo": "AC" }""", false)]
    [InlineData("""{ "$tipo": "reduzirTudo", "modalidadeCodigo": "AC" }""", false)]
    public void AjusteDoQuadro_PorTipo(string ajuste, bool valido)
    {
        ArgumentNullException.ThrowIfNull(ajuste);

        string[] erros = CaminhosComErro(Alterada("ajusteDoQuadro", Documento(ajuste)), Distribuicao);

        erros.Any(static e => e.StartsWith("/ajusteDoQuadro", StringComparison.Ordinal)).Should().Be(!valido);
    }

    [Fact(DisplayName = "A quantidade declarada negativa é recusada")]
    public void Modalidade_QuantidadeNegativa_Recusa() =>
        CaminhosComErro(Alterada("modalidades", Documento("""[ { "codigo": "AC", "quantidadeDeclarada": -1 } ]""")), Distribuicao)
            .Should().Contain("/modalidades/0/quantidadeDeclarada");

    [Theory(DisplayName = "O número de opções de curso fora de 1 e 2 é recusado")]
    [InlineData(0)]
    [InlineData(3)]
    public void OpcoesDeCurso_ForaDoVocabulario_Recusa(int numero) =>
        CaminhosComErro(Documento($$"""{ "formato": "uniplus.selecao.configuracao", "versaoFormato": 1, "opcoesDeCurso": { "numero": {{numero}} } }"""))
            .Should().Contain("/opcoesDeCurso/numero");

    [Theory(DisplayName = "A cascata com ordem informada, destino em minúsculas ou sem destino é recusada")]
    [InlineData("""{ "destinos": [ { "modalidadeOrigemCodigo": "A", "ordem": 1, "modalidadeDestinoCodigo": "B" } ] }""", "/destinos/0/ordem")]
    [InlineData("""{ "destinos": [ { "modalidadeOrigemCodigo": "A", "modalidadeDestinoCodigo": "b" } ] }""", "/destinos/0/modalidadeDestinoCodigo")]
    [InlineData("""{ "destinos": [ { "modalidadeOrigemCodigo": "A" } ] }""", "/destinos/0")]
    public void Cascata_Invalida_Recusa(string cascata, string caminho)
    {
        ArgumentNullException.ThrowIfNull(cascata);

        CaminhosComErro(Documento(cascata), Cascata).Should().Contain(caminho);
    }

    [Fact(DisplayName = "A cascata com 57 destinos é recusada")]
    public void Cascata_DestinosEmExcesso_Recusa()
    {
        JsonArray destinos = new([.. Enumerable.Range(1, 57).Select(static _ => (JsonNode)Documento("""{ "modalidadeOrigemCodigo": "A", "modalidadeDestinoCodigo": "B" }"""))]);

        CaminhosComErro(new JsonObject { ["destinos"] = destinos }, Cascata).Should().Contain("/destinos");
    }
}
