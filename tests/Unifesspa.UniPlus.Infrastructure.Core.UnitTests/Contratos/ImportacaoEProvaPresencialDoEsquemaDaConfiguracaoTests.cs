namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Contratos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using static Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Contratos.ContratoDaConfiguracao;

/// <summary>
/// A importação do resultado e a prova presencial no esquema da configuração: os relatórios entram pelo formato e pela
/// versão, a primeira fase depois da importação pela chave local, e o município de prova pelo código do IBGE.
/// </summary>
public sealed class ImportacaoEProvaPresencialDoEsquemaDaConfiguracaoTests
{
    private const string Importacao = "importacao.schema.json";

    private const string ProvaPresencial = "prova-presencial.schema.json";

    [Fact(DisplayName = "A importação com relatório por formato e versão e a primeira fase pela chave valida")]
    public void Importacao_PorFormatoEVersao_Valida() =>
        CaminhosComErro(Documento("""
            { "relatorios": [ { "formatoCodigo": "INEP-RESULTADO", "formatoVersao": "2" } ], "primeiraFaseDepoisDaImportacaoChave": "matricula" }
            """), Importacao).Should().BeEmpty();

    [Fact(DisplayName = "O formato pelo identificador interno e a fase fora do formato de chave são recusados")]
    public void Importacao_ReferenciaForaDoFormato_Recusa()
    {
        JsonNode importacao = Documento($$"""
            { "relatorios": [ { "formatoCodigo": "{{Guid.CreateVersion7()}}" } ], "primeiraFaseDepoisDaImportacaoChave": "Fase Final" }
            """);

        CaminhosComErro(importacao, Importacao).Should().Contain(["/relatorios/0/formatoCodigo", "/primeiraFaseDepoisDaImportacaoChave"]);
    }

    [Fact(DisplayName = "A prova presencial com municípios pelo código do IBGE valida")]
    public void ProvaPresencial_ComMunicipios_Valida() =>
        CaminhosComErro(Documento("""
            { "temProvaPresencial": true, "municipios": [ { "codigoIbge": "1505304", "uf": "PA", "nome": "Oriximiná" }, { "codigoIbge": "1508308", "uf": "PA" } ] }
            """), ProvaPresencial).Should().BeEmpty();

    [Fact(DisplayName = "O nome do município é só de leitura e opcional")]
    public void Municipio_Nome_EReadOnlyEOpcional()
    {
        JsonNode municipio = Esquema(ProvaPresencial)["$defs"]!["municipio"]!;

        municipio["properties"]!["nome"]!["readOnly"]!.GetValue<bool>().Should().BeTrue();
        municipio["required"]!.AsArray().Select(static item => item!.GetValue<string>()).Should().NotContain("nome");
    }

    [Fact(DisplayName = "Sem prova presencial a lista de municípios é recusada")]
    public void ProvaPresencial_SemProva_RecusaMunicipios() =>
        CaminhosComErro(Documento("""{ "temProvaPresencial": false, "municipios": [ { "codigoIbge": "1505304" } ] }"""), ProvaPresencial)
            .Should().Contain("/municipios");

    [Theory(DisplayName = "O município fora do formato do IBGE ou da UF é recusado")]
    [InlineData("""{ "codigoIbge": "15053" }""", "/municipios/0/codigoIbge")]
    [InlineData("""{ "codigoIbge": "1505304", "uf": "pa" }""", "/municipios/0/uf")]
    [InlineData("""{ "uf": "PA" }""", "/municipios/0")]
    [InlineData("""{ "codigoIbge": "9905304", "uf": "PA" }""", "/municipios/0/codigoIbge")]
    [InlineData("""{ "codigoIbge": "1505304" }""", "/municipios/0")]
    public void ProvaPresencial_MunicipioForaDoFormato_Recusa(string municipio, string caminho)
    {
        ArgumentNullException.ThrowIfNull(municipio);
        ArgumentNullException.ThrowIfNull(caminho);

        CaminhosComErro(Documento($$"""{ "temProvaPresencial": true, "municipios": [ {{municipio}} ] }"""), ProvaPresencial).Should().Contain(caminho);
    }

    [Fact(DisplayName = "A importação e a prova presencial ficam no documento")]
    public void Documento_ComImportacaoEProvaPresencial() =>
        CaminhosComErro(Documento("""
            { "formato": "uniplus.selecao.configuracao", "versaoFormato": 1,
              "importacao": { "relatorios": [ { "formatoCodigo": "INEP-RESULTADO", "formatoVersao": "2" } ] },
              "provaPresencial": { "temProvaPresencial": false } }
            """)).Should().BeEmpty();

    [Theory(DisplayName = "O relatório sem o formato ou sem a versão é recusado")]
    [InlineData("""{ }""")]
    [InlineData("""{ "formatoCodigo": "INEP-RESULTADO" }""")]
    [InlineData("""{ "formatoVersao": "2" }""")]
    public void Importacao_RelatorioIncompleto_Recusa(string relatorio)
    {
        ArgumentNullException.ThrowIfNull(relatorio);

        CaminhosComErro(Documento($$"""{ "relatorios": [ {{relatorio}} ] }"""), Importacao).Should().Contain("/relatorios/0");
    }
}
