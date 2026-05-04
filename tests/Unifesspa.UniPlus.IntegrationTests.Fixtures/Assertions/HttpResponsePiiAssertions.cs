namespace Unifesspa.UniPlus.IntegrationTests.Fixtures.Assertions;

using System.Text.Json;
using System.Text.RegularExpressions;

public static partial class HttpResponsePiiAssertions
{
    private static readonly (string Name, Regex Pattern)[] PiiPatterns =
    [
        ("nome + CPF combinados", NomeCpfRegex()),
        ("CPF não mascarado", CpfRegex()),
        ("e-mail completo", EmailRegex()),
    ];

    public static async Task AssertNoPiiAsync(this HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);
        string body = await response.Content.ReadAsStringAsync();
        AssertBodyNoPii(body);
    }

    public static void AssertBodyNoPii(string body)
    {
        if (string.IsNullOrEmpty(body))
            return;

        foreach ((string name, Regex pattern) in PiiPatterns)
        {
            if (!pattern.IsMatch(body))
                continue;

            string field = TryFindMatchingJsonField(body, pattern) ?? "(corpo da resposta)";
            Assert.Fail(
                $"PII detectado no campo '{field}' — padrão: {name}. " +
                "O response body não deve conter dados pessoais identificáveis.");
        }
    }

    private static string? TryFindMatchingJsonField(string body, Regex pattern)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(body);
            return FindMatchingField(doc.RootElement, pattern, path: null);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? FindMatchingField(JsonElement element, Regex pattern, string? path)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                string? value = element.GetString();
                return value is not null && pattern.IsMatch(value) ? path : null;

            case JsonValueKind.Object:
                foreach (JsonProperty prop in element.EnumerateObject())
                {
                    string childPath = path is null ? prop.Name : $"{path}.{prop.Name}";
                    string? found = FindMatchingField(prop.Value, pattern, childPath);
                    if (found is not null)
                        return found;
                }
                break;

            case JsonValueKind.Array:
                int index = 0;
                foreach (JsonElement item in element.EnumerateArray())
                {
                    string childPath = path is null ? $"[{index}]" : $"{path}[{index}]";
                    string? found = FindMatchingField(item, pattern, childPath);
                    if (found is not null)
                        return found;
                    index++;
                }
                break;

            default:
                break;
        }

        return null;
    }

    [GeneratedRegex(@"\d{3}\.\d{3}\.\d{3}-\d{2}")]
    private static partial Regex CpfRegex();

    [GeneratedRegex(@"\S+@\S+\.\S+")]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"(?:[A-Za-zÀ-ÿ]{2,} ){2,}\d{3}\.\d{3}\.\d{3}-\d{2}")]
    private static partial Regex NomeCpfRegex();
}
