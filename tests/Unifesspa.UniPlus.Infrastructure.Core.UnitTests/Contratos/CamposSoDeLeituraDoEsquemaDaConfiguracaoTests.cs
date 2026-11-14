namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Contratos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

/// <summary>
/// Todo descritivo e todo valor derivado que a exportação traz ao lado do código é só de leitura: o esquema o marca como
/// <c>readOnly</c> e a descrição diz que a importação o ignora e que a referência é o código.
/// </summary>
public sealed class CamposSoDeLeituraDoEsquemaDaConfiguracaoTests
{
    private static readonly JsonNode Esquema = JsonNode.Parse(File.ReadAllText(
        Path.Join(AppContext.BaseDirectory, "Contratos", "selecao-configuracao", "v1.schema.json")))!;

    public static TheoryData<string> CamposSoDeLeitura =>
    [
        "properties/criadoEm",
        "properties/atualizadoEm",
        "properties/tiposDeAtoCongelados",
        "$defs/identificacao/properties/localidadeNome",
        "$defs/identificacao/properties/localidadeUf",
        "$defs/identificacao/properties/tipoProcessoNome",
        "$defs/identificacao/properties/unidadeAdministradoraNome",
        "$defs/identificacao/properties/unidadeAdministradoraSigla",
        "$defs/fase/properties/unidadeResponsavelNome",
        "$defs/fase/properties/unidadeResponsavelSigla",
        "$defs/divulgacao/properties/regraNomeAbreviado",
    ];

    [Theory(DisplayName = "O campo só de leitura é marcado readOnly e a descrição diz que a importação o ignora")]
    [MemberData(nameof(CamposSoDeLeitura))]
    public void Campo_EReadOnlyComDescricao(string caminho)
    {
        ArgumentNullException.ThrowIfNull(caminho);
        JsonNode campo = Navegar(caminho);

        campo["readOnly"]!.GetValue<bool>().Should().BeTrue();
        campo["description"]!.GetValue<string>().Should().Contain("ignorado na importação");
    }

    [Theory(DisplayName = "O código de referência de cada descritivo não é readOnly")]
    [InlineData("$defs/identificacao/properties/localidadeCodigoIbge")]
    [InlineData("$defs/identificacao/properties/tipoProcessoCodigo")]
    [InlineData("$defs/identificacao/properties/unidadeAdministradoraCodigo")]
    [InlineData("$defs/fase/properties/unidadeResponsavelCodigo")]
    public void Codigo_NaoEReadOnly(string caminho)
    {
        ArgumentNullException.ThrowIfNull(caminho);

        Navegar(caminho)["readOnly"].Should().BeNull();
    }

    private static JsonNode Navegar(string caminho) =>
        caminho.Split('/').Aggregate(Esquema, static (no, passo) => no[passo]!);
}
