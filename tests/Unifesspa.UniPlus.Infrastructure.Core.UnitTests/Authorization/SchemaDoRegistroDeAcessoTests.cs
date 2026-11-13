namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Authorization;

using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Json.Schema;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Infrastructure.Core.Authorization;

using static Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Authorization.RegistroDeAcessoDeTeste;

/// <summary>
/// O schema versionado do valor da mensagem descreve o que o produtor serializa: o lote real valida contra ele, e os dois
/// conjuntos de propriedades coincidem, de modo que campo novo sem schema, ou campo do schema que deixou de sair, quebra
/// o teste.
/// </summary>
public sealed class SchemaDoRegistroDeAcessoTests
{
    // Path.Join só concatena: nenhum segmento posterior descarta o diretório base.
    private static readonly string CaminhoDoSchema = Path.Join(AppContext.BaseDirectory, "Contratos", "v1.schema.json");

    private static readonly JsonSchema Schema = JsonSchema.FromText(
        File.ReadAllText(CaminhoDoSchema));

    private static readonly EvaluationOptions Opcoes = new() { RequireFormatValidation = true };

    [Fact(DisplayName = "O lote real serializado valida contra o schema, com permitido, negado sem id de resource e impedido")]
    public void LoteReal_ValidaContraOSchema()
    {
        Guid inscricao = Guid.CreateVersion7();
        RegistroDecisaoAcesso[] lote =
        [
            Registro(clienteOidc: "selecao-web", resourceId: inscricao),
            Registro(resultado: ResultadoDoAcesso.Negado),
            Registro(resultado: ResultadoDoAcesso.Impedido),
        ];

        EvaluationResults resultado = Schema.Evaluate(JsonSerializer.SerializeToElement(JsonNode.Parse(RegistroDeAcessoKafka.ValorDoLote(lote))), Opcoes);

        resultado.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "O lote com o registro da leitura por titularidade valida contra o schema, sem propriedade não declarada")]
    public void LoteComTitularidade_ValidaContraOSchema()
    {
        JsonArray lote = (JsonArray)JsonNode.Parse(RegistroDeAcessoKafka.ValorDoLote([PorTitularidade(Guid.CreateVersion7())]))!;
        string[] declaradas = [.. PropriedadesDoSchema()["properties"]!.AsObject().Select(static p => p.Key)];

        Schema.Evaluate(JsonSerializer.SerializeToElement(lote), Opcoes).IsValid.Should().BeTrue();
        lote[0]!["registro"]!.AsObject().Select(static p => p.Key).Should().BeSubsetOf(declaradas);
    }

    [Fact(DisplayName = "A descrição de fonteGrant nomeia cada valor canônico da fonte, inclusive a titularidade")]
    public void FonteGrant_DescricaoNomeiaCadaValorCanonico()
    {
        string descricao = PropriedadesDoSchema()["properties"]!["fonteGrant"]!["description"]!.GetValue<string>();

        Enum.GetValues<FonteGrant>().Select(ValoresCanonicos.De)
            .Should().AllSatisfy(valor => descricao.Should().Contain($"`{valor}`"));
    }

    [Fact(DisplayName = "O schema recusa o registro sem um campo obrigatório")]
    public void RegistroSemCampoObrigatorio_NaoValida()
    {
        JsonArray lote = (JsonArray)JsonNode.Parse(RegistroDeAcessoKafka.ValorDoLote([Registro()]))!;
        lote[0]!["registro"]!.AsObject().Remove("resultado");

        Schema.Evaluate(JsonSerializer.SerializeToElement(lote), Opcoes).IsValid.Should().BeFalse();
    }

    [Fact(DisplayName = "O schema declara a versão do perfil da concessão como identificador, e recusa o que não é identificador")]
    public void VersaoId_EDeclaradoComoIdentificador()
    {
        JsonArray lote = (JsonArray)JsonNode.Parse(RegistroDeAcessoKafka.ValorDoLote([Registro()]))!;
        lote[0]!["registro"]!["versaoId"] = Guid.CreateVersion7().ToString();
        JsonArray invalido = (JsonArray)JsonNode.Parse(lote.ToJsonString())!;
        invalido[0]!["registro"]!["versaoId"] = "nao-e-identificador";

        Schema.Evaluate(JsonSerializer.SerializeToElement(lote), Opcoes).IsValid.Should().BeTrue();
        Schema.Evaluate(JsonSerializer.SerializeToElement(invalido), Opcoes).IsValid.Should().BeFalse();
    }

    [Fact(DisplayName = "O schema admite propriedade adicional: campo opcional novo não quebra o leitor da versão 1")]
    public void PropriedadeAdicional_Valida()
    {
        JsonArray lote = (JsonArray)JsonNode.Parse(RegistroDeAcessoKafka.ValorDoLote([Registro()]))!;
        lote[0]!["registro"]!["campoNovo"] = "valor";

        Schema.Evaluate(JsonSerializer.SerializeToElement(lote), Opcoes).IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "As propriedades serializadas do registro são as declaradas no schema, nos dois sentidos")]
    public void PropriedadesSerializadas_SaoAsDoSchema()
    {
        JsonArray lote = (JsonArray)JsonNode.Parse(RegistroDeAcessoKafka.ValorDoLote([Registro(clienteOidc: "selecao-web")]))!;
        string[] serializadas = [.. lote[0]!["registro"]!.AsObject().Select(static p => p.Key)];

        JsonObject documento = (JsonObject)JsonNode.Parse(File.ReadAllText(CaminhoDoSchema))!;
        string[] declaradas = [.. documento["$defs"]!["registro"]!["properties"]!.AsObject().Select(static p => p.Key)];

        serializadas.Should().BeEquivalentTo(declaradas);
        lote[0]!.AsObject().Select(static p => p.Key).Should().BeEquivalentTo(["chave", "registro"]);
    }

    [Fact(DisplayName = "O enum de motivoNegativa do schema é o vocabulário canônico de motivos, mais nulo")]
    public void MotivoNegativa_EnumDoSchemaEOVocabularioCanonico()
    {
        JsonObject documento = (JsonObject)JsonNode.Parse(File.ReadAllText(CaminhoDoSchema))!;
        string?[] declarados = [.. documento["$defs"]!["registro"]!["properties"]!["motivoNegativa"]!["enum"]!.AsArray()
            .Select(static v => v?.GetValue<string>())];

        string?[] canonicos = [.. Enum.GetValues<MotivoNegativa>().Select(static m => (string?)ValoresCanonicos.De(m)), null];

        declarados.Should().BeEquivalentTo(canonicos);
    }

    private static JsonObject PropriedadesDoSchema() =>
        (JsonObject)JsonNode.Parse(File.ReadAllText(CaminhoDoSchema))!["$defs"]!["registro"]!;
}
