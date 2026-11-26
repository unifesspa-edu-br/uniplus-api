namespace Unifesspa.UniPlus.Selecao.IntegrationTests.RolDeRegras;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Services.ExportacaoDaVersaoPublicada;
using Unifesspa.UniPlus.Selecao.Infrastructure.DocumentoDaConfiguracao;

/// <summary>
/// Os parâmetros do recurso que o documento da configuração aceita, na fase e na etapa: o prazo da 2ª instância, o efeito
/// suspensivo e a guarda da vaga. O esquema confere a forma; o valor aceito de cada código é do domínio.
/// </summary>
public sealed class ParametrosDoRecursoNoDocumentoTests
{
    private static readonly EsquemaDoDocumentoDaConfiguracao Esquema = new();

    [Theory(DisplayName = "O documento aceita o recurso sem prazo da 2ª instância e sem guarda da vaga, que o domínio completa ou recusa")]
    [InlineData(false)]
    [InlineData(true)]
    public void ParametrosOmitidos_Aceita(bool naFase) =>
        Esquema.Avaliar(Documento(naFase, Recurso())).Should().BeEmpty();

    [Theory(DisplayName = "O documento aceita o recurso com os parâmetros completos")]
    [InlineData(false)]
    [InlineData(true)]
    public void ParametrosCompletos_Aceita(bool naFase)
    {
        JsonObject recurso = Recurso();
        recurso["prazoSegundaInstanciaValor"] = 1.2345m;
        recurso["prazoSegundaInstanciaUnidade"] = "HORAS";
        recurso["guardaDaVaga"] = "ATE_O_RESULTADO_DEFINITIVO";

        Esquema.Avaliar(Documento(naFase, recurso)).Should().BeEmpty();
    }

    [Theory(DisplayName = "O documento aceita cada uma das três opções de efeito suspensivo, na fase e na etapa")]
    [InlineData("PARA_TODOS_OS_RECURSOS", false)]
    [InlineData("SEM_EFEITO_SUSPENSIVO", false)]
    [InlineData("UNIDADE_DECIDE_EM_CADA_RECURSO", false)]
    [InlineData("PARA_TODOS_OS_RECURSOS", true)]
    [InlineData("SEM_EFEITO_SUSPENSIVO", true)]
    [InlineData("UNIDADE_DECIDE_EM_CADA_RECURSO", true)]
    public void EfeitoSuspensivo_CadaOpcao_Aceita(string efeito, bool naFase)
    {
        JsonObject recurso = Recurso();
        recurso["efeitoSuspensivo"] = efeito;

        Esquema.Avaliar(Documento(naFase, recurso)).Should().BeEmpty();
    }

    [Theory(DisplayName = "A exportação da versão publicada mantém a opção de efeito suspensivo escolhida")]
    [InlineData("PARA_TODOS_OS_RECURSOS")]
    [InlineData("SEM_EFEITO_SUSPENSIVO")]
    [InlineData("UNIDADE_DECIDE_EM_CADA_RECURSO")]
    public void EfeitoSuspensivo_CadaOpcao_SobreviveAExportacao(string efeito)
    {
        string modelo = """
            {"tiposDeAto":[{"codigo":"RESULTADO","nome":"Resultado"}],"cronogramaFases":{"fases":[{"chave":"fase","codigo":"INSCRICAO","bancasRequeridas":[],
            "publicacoesPrevistas":[{"id":"produto","atoCodigo":"RESULTADO","papel":"PRELIMINAR"}],
            "regraRecurso":{"regra":{"codigo":"PRAZO","versao":"v1","hash":"hash"},"publicacaoDeReferenciaId":"produto",
            "parametros":{"prazoValor":"2.0000","prazoUnidade":"DIAS_UTEIS","admiteInstanciaSuperior":false,"efeitoSuspensivo":"EFEITO_DECLARADO","guardaDaVaga":"NAO_GUARDA"},
            "procedimentos":[]}}]}}
            """;
        string congelado = modelo.Replace("EFEITO_DECLARADO", efeito, StringComparison.Ordinal);

        JsonObject documento = new ProjecaoDaVersaoPublicada(Esquema).Projetar(new(Guid.Empty, 1, DateTimeOffset.UnixEpoch, congelado));

        documento["cronograma"]!["fases"]![0]!["regraRecurso"]!["efeitoSuspensivo"]!.GetValue<string>().Should().Be(efeito);
    }

    [Theory(DisplayName = "O documento aceita o código de dias corridos, para o domínio recusá-lo com a causa própria")]
    [InlineData("prazoUnidade")]
    [InlineData("prazoSegundaInstanciaUnidade")]
    public void DiasCorridos_ChegamAoDominio(string campo)
    {
        JsonObject recurso = Recurso();
        recurso[campo] = "DIAS";

        Esquema.Avaliar(Documento(false, recurso)).Should().BeEmpty();
    }

    [Theory(DisplayName = "O documento recusa no campo o tipo ou o código que o esquema não admite")]
    [InlineData("prazoSegundaInstanciaValor", "\"texto\"")]
    [InlineData("prazoSegundaInstanciaValor", "true")]
    [InlineData("prazoSegundaInstanciaValor", "1.23456")]
    [InlineData("prazoSegundaInstanciaUnidade", "\"meses\"")]
    [InlineData("prazoSegundaInstanciaUnidade", "7")]
    [InlineData("efeitoSuspensivo", "\"comEfeitoSuspensivo\"")]
    [InlineData("guardaDaVaga", "\"talvez\"")]
    [InlineData("guardaDaVaga", "1")]
    public void CodigoOuTipoInvalido_RecusaNoCampo(string campo, string json)
    {
        JsonObject recurso = Recurso();
        recurso[campo] = JsonNode.Parse(json);

        Esquema.Avaliar(Documento(false, recurso)).Select(c => c.Field)
            .Should().Contain($"/etapas/0/recursos/0/{campo}");
    }

    private static JsonObject Recurso() => new()
    {
        ["regraCodigo"] = "RECURSO_PRAZO_CONTADO_DA_PUBLICACAO_DO_ATO",
        ["regraVersao"] = "v1",
        ["admiteInstanciaSuperior"] = true,
        ["efeitoSuspensivo"] = "SEM_EFEITO_SUSPENSIVO",
    };

    private static JsonObject Documento(bool naFase, JsonObject recurso)
    {
        JsonObject documento = new() { ["formato"] = "uniplus.selecao.configuracao", ["versaoFormato"] = 1 };
        if (naFase)
        {
            documento["cronograma"] = new JsonObject
            {
                ["fases"] = new JsonArray(new JsonObject { ["chave"] = "inscricao", ["tipoFaseCodigo"] = "INSCRICAO", ["regraRecurso"] = recurso }),
            };
        }
        else
        {
            documento["etapas"] = new JsonArray(new JsonObject { ["chave"] = "prova", ["recursos"] = new JsonArray(recurso) });
        }

        return documento;
    }
}
