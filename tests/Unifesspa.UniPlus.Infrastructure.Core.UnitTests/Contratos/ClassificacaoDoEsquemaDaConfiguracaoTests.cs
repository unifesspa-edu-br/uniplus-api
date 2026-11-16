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
          "regraArredondamento": { "codigo": "PRECISAO-ARREDONDAR-CIMA", "versao": "v1" }, "casasArredondamento": 2,
          "escalaNotaFinal": 1000, "regraOrdemAlocacao": { "codigo": "ORDEM-POR-NOTA", "versao": "v1" }, "baseadoEmEnem": false,
          "regrasEliminacao": [
            { "regra": { "codigo": "ELIM-NOTA-MINIMA-ETAPA", "versao": "v1" }, "args": { "etapaChave": "prova-objetiva", "notaMinima": 30.5 } },
            { "regra": { "codigo": "ELIM-ZERO-EM-AREA", "versao": "v1" }, "args": {} } ],
          "criteriosDeDesempate": [
            { "ordem": 1, "regra": { "codigo": "DESEMPATE-MAIOR-NOTA-ETAPA", "versao": "v1" }, "args": { "etapaChave": "prova-objetiva" } },
            { "ordem": 2, "regra": { "codigo": "DESEMPATE-IDOSO", "versao": "v1" }, "args": { "idadeMinima": 60 } },
            { "ordem": 3, "regra": { "codigo": "DESEMPATE-PREDICADO-FATO", "versao": "v1" }, "args": { "fato": "UF", "operador": "EM", "valor": ["PA"] } },
            { "ordem": 4, "regra": { "codigo": "DESEMPATE-MAIOR-IDADE", "versao": "v1" }, "args": {} } ],
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
    [InlineData("DESEMPATE-MAIOR-NOTA-ETAPA", "{}", "/args")]
    [InlineData("DESEMPATE-MAIOR-NOTA-ETAPA", "{ \"etapaChave\": \"Prova Objetiva\" }", "/args/etapaChave")]
    [InlineData("DESEMPATE-IDOSO", "{ \"idadeMinima\": 0 }", "/args/idadeMinima")]
    [InlineData("DESEMPATE-MAIOR-NOTA-AREA-ENEM", "{ \"areas\": [] }", "/args/areas")]
    [InlineData("DESEMPATE-MAIOR-IDADE", "{ \"idadeMinima\": 60 }", "/args/idadeMinima")]
    [InlineData("DESEMPATE-PREDICADO-FATO", "{ \"fato\": \"UF\", \"operador\": \"EM\", \"valor\": \"PA\" }", "/args/valor")]
    public void Desempate_ParametroIncoerenteComARegra_Recusa(string codigo, string args, string caminho)
    {
        ArgumentNullException.ThrowIfNull(codigo);
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(caminho);
        JsonNode classificacao = Documento($$"""{ "criteriosDeDesempate": [ { "ordem": 1, "regra": { "codigo": "{{codigo}}", "versao": "v1" }, "args": {{args}} } ] }""");

        CaminhosComErro(classificacao, Classificacao).Should().Contain($"/criteriosDeDesempate/0{caminho}");
    }

    [Theory(DisplayName = "O parâmetro de eliminação que não é o da regra é recusado com o caminho")]
    [InlineData("ELIM-NOTA-MINIMA-ETAPA", "{ \"etapaChave\": \"prova\" }", "/args")]
    [InlineData("ELIM-CORTE-EM-AREA", "{ \"areaCodigo\": \"MAT\", \"minimo\": -1 }", "/args/minimo")]
    [InlineData("ELIM-AUSENCIA-NA-ETAPA", "{ \"etapaChave\": \"prova\", \"notaMinima\": 5 }", "/args/notaMinima")]
    [InlineData("ELIM-ZERO-EM-AREA", "{ \"areaCodigo\": \"MAT\" }", "/args/areaCodigo")]
    public void Eliminacao_ParametroIncoerenteComARegra_Recusa(string codigo, string args, string caminho)
    {
        ArgumentNullException.ThrowIfNull(codigo);
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(caminho);
        JsonNode classificacao = Documento($$"""{ "regrasEliminacao": [ { "regra": { "codigo": "{{codigo}}", "versao": "v1" }, "args": {{args}} } ] }""");

        CaminhosComErro(classificacao, Classificacao).Should().Contain($"/regrasEliminacao/0{caminho}");
    }

    [Fact(DisplayName = "A regra de código desconhecido aceita parâmetros livres, porque a variante nova nasce no catálogo")]
    public void Regra_DeCodigoDesconhecido_AceitaParametros() =>
        CaminhosComErro(Documento("""{ "regrasEliminacao": [ { "regra": { "codigo": "ELIM-NOVA", "versao": "v1" }, "args": { "x": 1 } } ] }"""), Classificacao)
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
        CaminhosComErro(Documento("""{ "regrasEliminacao": [ { "regra": { "codigo": "ELIM-CORTE-EM-AREA" }, "args": { "areaCodigo": "mat", "minimo": 1 } } ] }"""), Classificacao)
            .Should().Contain("/regrasEliminacao/0/args/areaCodigo");
        CaminhosComErro(Documento("""{ "criteriosDeDesempate": [ { "regra": { "codigo": "DESEMPATE-MAIOR-NOTA-AREA-ENEM" }, "args": { "areas": [ "MAT-1" ] } } ] }"""), Classificacao)
            .Should().Contain("/criteriosDeDesempate/0/args/areas/0");
        CaminhosComErro(Documento("""{ "quadroPesoAreaEnem": [ { "areas": [ { "codigo": "MAT", "corte": 1001 } ] } ] }"""), Classificacao)
            .Should().Contain("/quadroPesoAreaEnem/0/areas/0/corte");
    }
}
