namespace Unifesspa.UniPlus.Selecao.IntegrationTests.RolDeRegras;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Infrastructure.DocumentoDaConfiguracao;

public sealed class SuspensividadeNoDocumentoTests
{
    private static readonly EsquemaDoDocumentoDaConfiguracao Esquema = new();

    [Theory(DisplayName = "O documento admite null explícito para desativar a suspensividade em cada instância")]
    [InlineData(false, "Primeira")]
    [InlineData(false, "Segunda")]
    [InlineData(true, "Primeira")]
    [InlineData(true, "Segunda")]
    public void Suspensividade_ParNulo_Aceita(bool naFase, string instancia)
    {
        JsonObject recurso = Recurso();
        recurso[$"suspensividade{instancia}InstanciaValor"] = null;
        recurso[$"suspensividade{instancia}InstanciaUnidade"] = null;

        Esquema.Avaliar(Documento(naFase, recurso)).Should().BeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Suspensividade_ParametrosOmitidos_Aceita(bool naFase) =>
        Esquema.Avaliar(Documento(naFase, Recurso())).Should().BeEmpty();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Suspensividade_ParesPreenchidos_Aceita(bool naFase)
    {
        JsonObject recurso = Recurso();
        recurso["suspensividadePrimeiraInstanciaValor"] = 1.2345m;
        recurso["suspensividadePrimeiraInstanciaUnidade"] = "horas";
        recurso["suspensividadeSegundaInstanciaValor"] = 2m;
        recurso["suspensividadeSegundaInstanciaUnidade"] = "diasUteis";

        Esquema.Avaliar(Documento(naFase, recurso)).Should().BeEmpty();
    }

    [Theory]
    [InlineData("Primeira", "Valor", "\"texto\"")]
    [InlineData("Segunda", "Valor", "true")]
    [InlineData("Primeira", "Valor", "1.23456")]
    [InlineData("Segunda", "Valor", "1.23456")]
    [InlineData("Primeira", "Unidade", "\"meses\"")]
    [InlineData("Segunda", "Unidade", "7")]
    public void Suspensividade_TipoOuUnidadeInvalida_RecusaNoCampo(string instancia, string parte, string json)
    {
        string campo = $"suspensividade{instancia}Instancia{parte}";
        JsonObject recurso = Recurso();
        recurso[campo] = JsonNode.Parse(json);

        Esquema.Avaliar(Documento(false, recurso)).Select(c => c.Field)
            .Should().Contain($"/etapas/0/recursos/0/{campo}");
    }

    private static JsonObject Recurso() => new()
    {
        ["regraCodigo"] = "RECURSO-PRAZO-ANCORADO-EM-ATO",
        ["regraVersao"] = "v1",
        ["admiteInstanciaSuperior"] = true,
    };

    private static JsonObject Documento(bool naFase, JsonObject recurso)
    {
        JsonObject documento = new() { ["formato"] = "uniplus.selecao.configuracao", ["versaoFormato"] = 1 };
        if (naFase)
        {
            documento["cronograma"] = new JsonObject
            {
                ["fases"] = new JsonArray(new JsonObject { ["chave"] = "inscricao", ["faseCanonicaCodigo"] = "INSCRICAO", ["regraRecurso"] = recurso }),
            };
        }
        else
        {
            documento["etapas"] = new JsonArray(new JsonObject { ["chave"] = "prova", ["recursos"] = new JsonArray(recurso) });
        }
        return documento;
    }
}
