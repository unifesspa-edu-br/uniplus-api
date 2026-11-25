namespace Unifesspa.UniPlus.IntegrationTests.Fixtures.Assertions;

using System.Text.Json;
using System.Text.RegularExpressions;

public static partial class HttpResponsePiiAssertions
{
    private static readonly (string Name, Regex Pattern)[] PiiPatterns =
    [
        ("nome + CPF combinados", NomeCpfRegex()),
        ("CPF não mascarado (formatado)", CpfRegex()),
        ("CPF não mascarado (sem formatação)", CpfCruRegex()),
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
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() is string value && pattern.IsMatch(value) ? path : null,
            JsonValueKind.Object => FindMatchingProperty(element, pattern, path),
            JsonValueKind.Array => FindMatchingItem(element, pattern, path),
            _ => null,
        };
    }

    private static string? FindMatchingProperty(JsonElement element, Regex pattern, string? path)
    {
        foreach (JsonProperty prop in element.EnumerateObject())
        {
            string childPath = path is null ? prop.Name : $"{path}.{prop.Name}";
            string? found = FindMatchingField(prop.Value, pattern, childPath);
            if (found is not null)
                return found;
        }

        return null;
    }

    private static string? FindMatchingItem(JsonElement element, Regex pattern, string? path)
    {
        int index = 0;
        foreach (JsonElement item in element.EnumerateArray())
        {
            string childPath = path is null ? $"[{index}]" : $"{path}[{index}]";
            string? found = FindMatchingField(item, pattern, childPath);
            if (found is not null)
                return found;
            index++;
        }

        return null;
    }

    [GeneratedRegex(@"\d{3}\.\d{3}\.\d{3}-\d{2}")]
    private static partial Regex CpfRegex();

    // CPF cru: 11 dígitos não adjacentes a qualquer char hex (0-9 a-f A-F),
    // evitando falsos positivos em traceId e segmentos de UUID.
    [GeneratedRegex(@"(?<![0-9a-fA-F])\d{11}(?![0-9a-fA-F])")]
    private static partial Regex CpfCruRegex();

    [GeneratedRegex(@"\S+@\S+\.\S+")]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"(?:[A-Za-zÀ-ÿ]{2,} ){2,}\d{3}\.\d{3}\.\d{3}-\d{2}")]
    private static partial Regex NomeCpfRegex();
}
