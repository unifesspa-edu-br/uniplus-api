namespace Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// As opções de serialização com que o host publica e recebe JSON — camelCase nos nomes de
/// propriedade e nos membros de enum. Testes que desserializam uma resposta em DTO do
/// contrato precisam usar estas, não as opções default de <c>ReadFromJsonAsync</c>.
/// </summary>
/// <remarks>
/// As default de <c>System.Net.Http.Json</c> não registram
/// <see cref="JsonStringEnumConverter"/>, então um campo de enum publicado como
/// <c>"rascunho"</c> falha ao ser lido de volta. Enquanto todo enum saía como
/// <c>string</c> na projeção, a diferença não aparecia — e é justamente essa folga que
/// deixava a assimetria de vocabulário passar despercebida pela suíte (issue #1294).
/// </remarks>
public static class JsonDoContrato
{
    /// <summary>Espelha o que <c>Program.cs</c> configura em <c>AddJsonOptions</c>/<c>ConfigureHttpJsonOptions</c>.</summary>
    public static JsonSerializerOptions Opcoes { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };
}
