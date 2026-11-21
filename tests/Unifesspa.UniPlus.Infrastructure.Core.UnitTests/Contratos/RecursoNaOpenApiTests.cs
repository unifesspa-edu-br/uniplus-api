namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Contratos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

/// <summary>
/// O que a OpenAPI do módulo Seleção publica para quem monta o recurso: as três leituras de vocabulário, o efeito suspensivo e a guarda da
/// vaga obrigatórios e sem valor nulo na gravação, e a lista de erros com a identificação do item.
/// </summary>
public sealed class RecursoNaOpenApiTests
{
    private static readonly JsonNode Baseline = JsonNode.Parse(File.ReadAllText(Path.Join(AppContext.BaseDirectory, "Contratos", "openapi.selecao.json")))!;

    private static JsonObject Componentes => Baseline["components"]!["schemas"]!.AsObject();

    [Theory(DisplayName = "A leitura de vocabulário do recurso é só um GET que devolve a lista do componente, com descrição quando o enum a tem")]
    [InlineData("/api/selecao/unidades-do-prazo-de-recurso", "UnidadePrazo", false)]
    [InlineData("/api/selecao/efeitos-suspensivos", "EfeitoSuspensivoComDescricao", true)]
    [InlineData("/api/selecao/guardas-da-vaga", "GuardaDaVagaComDescricao", true)]
    public void Vocabulario_EstaNosCaminhosEDevolveALista(string caminho, string componente, bool comDescricao)
    {
        JsonObject operacoes = Baseline["paths"]![caminho]?.AsObject()
            ?? throw new InvalidOperationException($"{caminho} não está em paths.");

        operacoes.Select(static o => o.Key).Should().Equal("get");
        operacoes["get"]!.ToJsonString().Should().Contain($"#/components/schemas/{componente}");
        Componentes[componente]!["required"]!.AsArray().Select(static n => n!.GetValue<string>()).Should().Equal(comDescricao ? ["codigo", "rotulo", "descricao"] : ["codigo", "rotulo"]);
    }

    [Fact(DisplayName = "Na leitura dos parâmetros do recurso, o efeito suspensivo e a guarda da vaga são obrigatórios e sem valor nulo")]
    public void EfeitoEGuarda_NaLeitura_SaoObrigatoriosESemNulo()
    {
        JsonNode esquema = Componentes["ParametrosRegraPrazoRecursoDto"]!;

        esquema["required"]!.AsArray().Select(static n => n!.GetValue<string>()).Should().Contain(["efeitoSuspensivo", "guardaDaVaga"]);
        esquema["properties"]!["efeitoSuspensivo"]!.ToJsonString().Should().NotContain("\"null\"");
        esquema["properties"]!["guardaDaVaga"]!.ToJsonString().Should().NotContain("\"null\"");
    }

    [Fact(DisplayName = "As leituras do recurso publicam a unidade do prazo pelo código, e não pelo número do enum")]
    public void UnidadeDoPrazo_NaLeitura_EOCodigo()
    {
        Componentes["UnidadePrazoCodigo"]!["type"]!.GetValue<string>().Should().Be("string");
        Componentes["UnidadePrazoCodigo"]!["enum"]!.AsArray().Select(static n => n!.GetValue<string>()).Should().Contain(["horas", "diasUteis"]);
    }

    [Fact(DisplayName = "O erro do problema publica a lista de erros com o campo, o código, a mensagem e a identificação do item")]
    public void ProblemDetails_PublicaAIdentificacaoDoItem()
    {
        JsonNode erro = Componentes["ProblemDetails"]!["properties"]!["errors"]!["items"]!;
        JsonNode identificador = erro["properties"]!["identificadores"]!["items"]!;

        erro["required"]!.AsArray().Select(static n => n!.GetValue<string>()).Should().Equal("code", "message");
        erro["properties"]!.AsObject().Select(static p => p.Key).Should().Equal("field", "code", "message", "identificadores");
        identificador["required"]!.AsArray().Select(static n => n!.GetValue<string>()).Should().Equal("lista", "identificador");
    }
}
