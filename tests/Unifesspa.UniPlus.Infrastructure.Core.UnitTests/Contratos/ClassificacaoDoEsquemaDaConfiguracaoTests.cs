namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Contratos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using static Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Contratos.ContratoDaConfiguracao;

/// <summary>
/// A classificação no esquema da configuração: as regras do catálogo entram por código e versão, os parâmetros de cada regra
/// de desempate e de eliminação seguem o código dela, as etapas são citadas pela chave local e o quadro de pesos por área é
/// só de leitura.
/// </summary>
public sealed class ClassificacaoDoEsquemaDaConfiguracaoTests
{
    private const string Classificacao = "classificacao.schema.json";

    private const string ClassificacaoCompleta = """
        { "regraCalculo": { "codigo": "MEDIA-PONDERADA", "versao": "v1" },
          "regraArredondamento": { "codigo": "PRECISAO_ARREDONDAR_A_PARTIR_DE_CINCO", "versao": "v1" }, "casasArredondamento": 2,
          "escalaNotaFinal": 1000, "regraOrdemAlocacao": { "codigo": "ORDEM-POR-NOTA", "versao": "v1" }, "baseadoEmEnem": false,
          "regrasEliminacao": [
            { "regra": { "codigo": "ELIMINACAO_NOTA_MINIMA_ETAPA", "versao": "v1" }, "parametros": { "etapaChave": "etapa_prova_objetiva", "notaMinima": 30.5 } },
            { "regra": { "codigo": "ELIMINACAO_CORTE_EM_AREA", "versao": "v1" }, "parametros": { "areaCodigo": "REDACAO", "minimo": 400 } },
            { "regra": { "codigo": "ELIMINACAO_AUSENCIA_NA_ETAPA", "versao": "v1" }, "parametros": { "etapaChave": "etapa_prova_objetiva" } },
            { "regra": { "codigo": "ELIMINACAO_ZERO_EM_AREA", "versao": "v1" }, "parametros": {} },
            { "regra": { "codigo": "ELIMINACAO_FALTA_EM_DIA_DE_PROVA_ENEM", "versao": "v1" }, "parametros": {} } ],
          "criteriosDeDesempate": [
            { "ordem": 1, "regra": { "codigo": "DESEMPATE_MAIOR_NOTA_ETAPA", "versao": "v1" }, "parametros": { "etapaChave": "etapa_prova_objetiva" } },
            { "ordem": 2, "regra": { "codigo": "DESEMPATE_IDOSO", "versao": "v1" }, "parametros": { "idadeMinima": 60 } },
            { "ordem": 3, "regra": { "codigo": "DESEMPATE_CONDICAO_SOBRE_DADO", "versao": "v1" }, "parametros": { "codigoDoDado": "UF", "operador": "ALGUM_DE", "valor": ["PA"] } },
            { "ordem": 4, "regra": { "codigo": "DESEMPATE_MAIOR_NOTA_AREA_ENEM", "versao": "v1" }, "parametros": { "areas": [ "MATEMATICA", "REDACAO" ] } },
            { "ordem": 5, "regra": { "codigo": "DESEMPATE_MAIOR_IDADE", "versao": "v1" }, "parametros": {} } ],
          "resolucaoPesoAreaEnem": "RES-2026-01" }
        """;

    [Fact(DisplayName = "A classificação completa valida e a vazia, como num rascunho, também")]
    public void Classificacao_CompletaEVazia_Valida()
    {
        CaminhosComErro(Documento(ClassificacaoCompleta), Classificacao).Should().BeEmpty();
        CaminhosComErro(Documento("{}"), Classificacao).Should().BeEmpty();
    }

    [Fact(DisplayName = "A regra citada pelo identificador interno, sem código e versão, é recusada")]
    public void Regra_SemCodigoEVersao_Recusa() =>
        CaminhosComErro(Documento("""{ "regraCalculo": { "id": "3f2a9c1e-5b7d-4e8a-9c3f-1a2b3c4d5e6f" } }"""), Classificacao)
            .Should().Contain("/regraCalculo/id");

    [Theory(DisplayName = "O parâmetro de desempate que não é o da regra é recusado com o caminho")]
    [InlineData("DESEMPATE_MAIOR_NOTA_ETAPA", "{}", "/parametros")]
    [InlineData("DESEMPATE_MAIOR_NOTA_ETAPA", "{ \"etapaChave\": \"Prova Objetiva\" }", "/parametros/etapaChave")]
    [InlineData("DESEMPATE_IDOSO", "{ \"idadeMinima\": 0 }", "/parametros/idadeMinima")]
    [InlineData("DESEMPATE_MAIOR_NOTA_AREA_ENEM", "{ \"areas\": [] }", "/parametros/areas")]
    [InlineData("DESEMPATE_MAIOR_IDADE", "{ \"idadeMinima\": 60 }", "/parametros/idadeMinima")]
    [InlineData("DESEMPATE_CONDICAO_SOBRE_DADO", "{ \"codigoDoDado\": \"UF\", \"operador\": \"ALGUM_DE\", \"valor\": \"PA\" }", "/parametros/valor")]
    [InlineData("DESEMPATE_MAIOR_NOTA_ETAPA", "{ \"etapaChave\": \"prova\", \"invento\": 1 }", "/parametros/invento")]
    [InlineData("DESEMPATE_IDOSO", "{ \"idadeMinima\": 60, \"invento\": 1 }", "/parametros/invento")]
    [InlineData("DESEMPATE_MAIOR_NOTA_AREA_ENEM", "{ \"areas\": [ \"MAT\" ], \"invento\": 1 }", "/parametros/invento")]
    [InlineData("DESEMPATE_CONDICAO_SOBRE_DADO", "{ \"codigoDoDado\": \"UF\", \"operador\": \"ALGUM_DE\", \"valor\": [ \"PA\" ], \"invento\": 1 }", "/parametros/invento")]
    public void Desempate_ParametroIncoerenteComARegra_Recusa(string codigo, string parametros, string caminho)
    {
        ArgumentNullException.ThrowIfNull(codigo);
        ArgumentNullException.ThrowIfNull(parametros);
        ArgumentNullException.ThrowIfNull(caminho);
        JsonNode classificacao = Documento($$"""{ "criteriosDeDesempate": [ { "ordem": 1, "regra": { "codigo": "{{codigo}}", "versao": "v1" }, "parametros": {{parametros}} } ] }""");

        CaminhosComErro(classificacao, Classificacao).Should().Contain($"/criteriosDeDesempate/0{caminho}");
    }

    [Theory(DisplayName = "O parâmetro de eliminação que não é o da regra é recusado com o caminho")]
    [InlineData("ELIMINACAO_NOTA_MINIMA_ETAPA", "{ \"etapaChave\": \"prova\" }", "/parametros")]
    [InlineData("ELIMINACAO_CORTE_EM_AREA", "{ \"areaCodigo\": \"MAT\", \"minimo\": -1 }", "/parametros/minimo")]
    [InlineData("ELIMINACAO_AUSENCIA_NA_ETAPA", "{ \"etapaChave\": \"prova\", \"notaMinima\": 5 }", "/parametros/notaMinima")]
    [InlineData("ELIMINACAO_ZERO_EM_AREA", "{ \"areaCodigo\": \"MAT\" }", "/parametros/areaCodigo")]
    [InlineData("ELIMINACAO_NOTA_MINIMA_ETAPA", "{ \"etapaChave\": \"prova\", \"notaMinima\": 5, \"invento\": 1 }", "/parametros/invento")]
    [InlineData("ELIMINACAO_CORTE_EM_AREA", "{ \"areaCodigo\": \"MAT\", \"minimo\": 5, \"invento\": 1 }", "/parametros/invento")]
    [InlineData("ELIMINACAO_AUSENCIA_NA_ETAPA", "{ \"etapaChave\": \"prova\", \"invento\": 1 }", "/parametros/invento")]
    [InlineData("ELIMINACAO_FALTA_EM_DIA_DE_PROVA_ENEM", "{ \"invento\": 1 }", "/parametros/invento")]
    public void Eliminacao_ParametroIncoerenteComARegra_Recusa(string codigo, string parametros, string caminho)
    {
        ArgumentNullException.ThrowIfNull(codigo);
        ArgumentNullException.ThrowIfNull(parametros);
        ArgumentNullException.ThrowIfNull(caminho);
        JsonNode classificacao = Documento($$"""{ "regrasEliminacao": [ { "regra": { "codigo": "{{codigo}}", "versao": "v1" }, "parametros": {{parametros}} } ] }""");

        CaminhosComErro(classificacao, Classificacao).Should().Contain($"/regrasEliminacao/0{caminho}");
    }

    [Fact(DisplayName = "A regra de código desconhecido aceita parâmetros livres, porque a variante nova nasce no catálogo")]
    public void Regra_DeCodigoDesconhecido_AceitaParametros() =>
        CaminhosComErro(Documento("""{ "regrasEliminacao": [ { "regra": { "codigo": "ELIMINACAO-NOVA", "versao": "v1" }, "parametros": { "x": 1 } } ] }"""), Classificacao)
            .Should().BeEmpty();

    [Theory(DisplayName = "A escala da nota final fora do intervalo ou com casas demais é recusada")]
    [InlineData("0")]
    [InlineData("100000")]
    [InlineData("10.00001")]
    public void EscalaDaNotaFinal_ForaDoIntervalo_Recusa(string escala) =>
        CaminhosComErro(Documento($$"""{ "escalaNotaFinal": {{escala}} }"""), Classificacao).Should().Contain("/escalaNotaFinal");

    [Fact(DisplayName = "A classificação fica no documento e o quadro de pesos por área é aceito como dado de leitura")]
    public void Documento_ComClassificacao()
    {
        JsonNode documento = Documento($$"""
            { "formato": "uniplus.selecao.configuracao", "versaoFormato": 1,
              "classificacao": { "resolucaoPesoAreaEnem": "RES-2026-01", "quadroPesoAreaEnem": [
                { "grupoAreaEnemCodigo": "GRUPO-1", "grupoAreaEnemRotulo": "Grupo 1", "baseLegal": "Resolução 1/2026",
                  "areas": [ { "codigo": "MAT", "rotulo": "Matemática", "peso": 3.5, "corte": 450 } ] } ] } }
            """);

        CaminhosComErro(documento).Should().BeEmpty();
    }

    [Fact(DisplayName = "Zero casas de arredondamento, código de área fora do formato e corte acima de 1000 são recusados")]
    public void Classificacao_LimitesDoDominio_Recusa()
    {
        CaminhosComErro(Documento("""{ "casasArredondamento": 0 }"""), Classificacao).Should().Contain("/casasArredondamento");
        CaminhosComErro(Documento("""{ "regrasEliminacao": [ { "regra": { "codigo": "ELIMINACAO_CORTE_EM_AREA" }, "parametros": { "areaCodigo": "mat", "minimo": 1 } } ] }"""), Classificacao)
            .Should().Contain("/regrasEliminacao/0/parametros/areaCodigo");
        CaminhosComErro(Documento("""{ "criteriosDeDesempate": [ { "regra": { "codigo": "DESEMPATE_MAIOR_NOTA_AREA_ENEM" }, "parametros": { "areas": [ "MAT-1" ] } } ] }"""), Classificacao)
            .Should().Contain("/criteriosDeDesempate/0/parametros/areas/0");
        CaminhosComErro(Documento("""{ "quadroPesoAreaEnem": [ { "areas": [ { "codigo": "MAT", "corte": 1001 } ] } ] }"""), Classificacao)
            .Should().Contain("/quadroPesoAreaEnem/0/areas/0/corte");
    }
}
