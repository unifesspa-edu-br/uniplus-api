namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Contratos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using static Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Contratos.ContratoDaConfiguracao;

/// <summary>
/// O atendimento especializado e o bônus regional no esquema da configuração: cada item entra pelo código estável do
/// catálogo, o nome e o que a base legal congela são só de leitura, e a declaração de não aplicar o bônus proíbe os parâmetros dele.
/// </summary>
public sealed class AtendimentoEBonusDoEsquemaDaConfiguracaoTests
{
    private const string Atendimento = "atendimento.schema.json";

    private const string Bonus = "fada.schema.json";

    [Fact(DisplayName = "O atendimento com condição, recurso e tipo de deficiência pelo código valida")]
    public void Atendimento_PorCodigo_Valida() =>
        CaminhosComErro(Documento("""
            { "condicoes": [ { "condicaoCodigo": "PCD", "condicaoNome": "Pessoa com deficiência" } ],
              "recursos": [ { "recursoCodigo": "LEDOR" } ], "tiposDeficiencia": [ { "tipoDeficienciaCodigo": "VISUAL" } ] }
            """), Atendimento).Should().BeEmpty();

    [Theory(DisplayName = "O código fora do formato do cadastro ou o item sem código é recusado")]
    [InlineData("condicoes", "condicaoCodigo", "pcd")]
    [InlineData("recursos", "recursoCodigo", "1LEDOR")]
    [InlineData("tiposDeficiencia", "tipoDeficienciaCodigo", "V")]
    public void Atendimento_CodigoForaDoFormato_Recusa(string lista, string propriedade, string codigo)
    {
        ArgumentNullException.ThrowIfNull(lista);
        ArgumentNullException.ThrowIfNull(propriedade);
        ArgumentNullException.ThrowIfNull(codigo);
        JsonNode atendimento = Documento($$"""{ "{{lista}}": [ { "{{propriedade}}": "{{codigo}}" } ] }""");

        CaminhosComErro(atendimento, Atendimento).Should().Contain($"/{lista}/0/{propriedade}");
        CaminhosComErro(Documento($$"""{ "{{lista}}": [ { } ] }"""), Atendimento).Should().Contain($"/{lista}/0");
    }

    [Fact(DisplayName = "O bônus que aplica exige regra, fator e base legal pelo código")]
    public void Bonus_Aplica_ExigeRegraFatorEBaseLegal()
    {
        JsonNode completo = Documento("""
            { "aplica": true, "regra": { "codigo": "FADA_MULTIPLICATIVO", "versao": "v1" }, "fator": 1.2, "teto": 50.5,
              "codigoDoDadoDeElegibilidade": "MUNICIPIO_RESIDENCIA", "baseLegalCodigo": "LEI_REGIONAL",
              "tipoInstrumento": "LEI", "identificacao": "Lei 1/2020", "descricao": "Bônus para municípios do sul e sudeste do Pará.",
              "municipios": [ { "codigoIbge": "1505304", "nome": "Marabá", "uf": "PA" } ] }
            """);

        CaminhosComErro(completo, Bonus).Should().BeEmpty();
        CaminhosComErro(Documento("""{ "aplica": true }"""), Bonus).Should().NotBeEmpty();
    }

    [Fact(DisplayName = "O bônus que não se aplica é só a declaração e recusa os parâmetros")]
    public void Bonus_NaoAplica_RecusaParametros()
    {
        CaminhosComErro(Documento("""{ "aplica": false }"""), Bonus).Should().BeEmpty();
        CaminhosComErro(Documento("""{ "aplica": false, "fator": 1.2 }"""), Bonus).Should().Contain("/fator");
        CaminhosComErro(Documento("""{ "aplica": false, "baseLegalCodigo": "LEI_REGIONAL" }"""), Bonus).Should().Contain("/baseLegalCodigo");
    }

    [Theory(DisplayName = "A base legal e o município fora do formato do cadastro são recusados")]
    [InlineData("""{ "aplica": true, "regra": { "codigo": "B" }, "fator": 1.2, "baseLegalCodigo": "lei regional" }""", "/baseLegalCodigo")]
    [InlineData("""{ "aplica": true, "regra": { "codigo": "B" }, "fator": 1.2, "baseLegalCodigo": "LEI", "municipios": [ { "codigoIbge": "15053" } ] }""", "/municipios/0/codigoIbge")]
    [InlineData("""{ "aplica": true, "regra": { "codigo": "B" }, "fator": -1, "baseLegalCodigo": "LEI" }""", "/fator")]
    public void Bonus_FormaInvalida_Recusa(string json, string caminho)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(caminho);

        CaminhosComErro(Documento(json), Bonus).Should().Contain(caminho);
    }

    [Fact(DisplayName = "O atendimento e o bônus ficam no documento")]
    public void Documento_ComAtendimentoEBonus()
    {
        JsonNode documento = Documento("""
            { "formato": "uniplus.selecao.configuracao", "versaoFormato": 1,
              "atendimento": { "condicoes": [ { "condicaoCodigo": "PCD" } ] }, "fada": { "aplica": false } }
            """);

        CaminhosComErro(documento).Should().BeEmpty();
    }

    [Theory(DisplayName = "O fator ou o teto zero ou acima do que a coluna comporta é recusado")]
    [InlineData("fator", "0")]
    [InlineData("fator", "100")]
    [InlineData("teto", "0")]
    [InlineData("teto", "100.5")]
    public void Bonus_FatorETetoForaDoIntervalo_Recusa(string propriedade, string valor)
    {
        ArgumentNullException.ThrowIfNull(propriedade);
        ArgumentNullException.ThrowIfNull(valor);
        JsonNode bonus = Documento("""{ "aplica": true, "regra": { "codigo": "B" }, "fator": 1.2, "baseLegalCodigo": "LEI" }""");
        bonus[propriedade] = JsonNode.Parse(valor);

        CaminhosComErro(bonus, Bonus).Should().Contain($"/{propriedade}");
    }

    [Fact(DisplayName = "O bônus que não se aplica recusa também os descritivos congelados da base legal")]
    public void Bonus_NaoAplica_RecusaDescritivosDaBaseLegal() =>
        CaminhosComErro(Documento("""{ "aplica": false, "tipoInstrumento": "LEI", "municipios": [] }"""), Bonus)
            .Should().Contain(["/tipoInstrumento", "/municipios"]);
}
