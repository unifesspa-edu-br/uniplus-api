namespace Unifesspa.UniPlus.IntegrationTests.Fixtures;

using System.Text.Json;

/// <summary>
/// Confere, no documento OpenAPI servido, que o campo de texto que carrega o código de um enum de contrato (ADR-0176)
/// referencia o componente <c>{Enum}Codigo</c> em vez de sair como texto livre.
/// </summary>
public static class CodigosDeEnumNoContrato
{
    private static readonly Dictionary<string, string> ComponentePorCampo = new(StringComparer.Ordinal)
    {
        ["operador"] = "OperadorCodigo",
        ["tipoDeCampo"] = "TipoDeCampoCodigo",
        ["visivel"] = "TernarioCodigo",
        ["obrigatorio"] = "TernarioCodigo",
        ["impedido"] = "TernarioCodigo",
        ["estado"] = "EstadoFatoCodigo",
    };

    /// <summary>
    /// Os campos do documento, no formato <c>Schema.campo</c>, cujo nome é o de um campo que leva o código de um enum e
    /// que saem como texto cru. O campo booleano de mesmo nome, como o de documento obrigatório, não conta.
    /// </summary>
    public static IReadOnlyList<string> CamposComTextoCru(JsonElement documento) =>
    [
        .. documento.GetProperty("components").GetProperty("schemas").EnumerateObject()
            .Where(static schema => schema.Value.TryGetProperty("properties", out _))
            .SelectMany(static schema => schema.Value.GetProperty("properties").EnumerateObject()
                .Where(static campo => ComponentePorCampo.ContainsKey(campo.Name) && EhTextoCru(campo.Value))
                .Select(campo => $"{schema.Name}.{campo.Name}")),
    ];

    /// <summary>O campo, no formato <c>Schema.campo</c>, referencia o componente dos códigos do enum dele.</summary>
    public static bool ReferenciaOsCodigos(JsonElement documento, string schema, string campo)
    {
        JsonElement definicao = documento.GetProperty("components").GetProperty("schemas")
            .GetProperty(schema).GetProperty("properties").GetProperty(campo);

        return Referencias(definicao).Any(referencia => referencia.EndsWith($"/{ComponentePorCampo[campo]}", StringComparison.Ordinal));
    }

    private static bool EhTextoCru(JsonElement campo) =>
        !Referencias(campo).Any() && campo.TryGetProperty("type", out JsonElement tipo) && (tipo.ValueKind == JsonValueKind.Array
            ? tipo.EnumerateArray().Any(static t => t.GetString() == "string")
            : tipo.GetString() == "string");

    private static IEnumerable<string> Referencias(JsonElement campo)
    {
        if (campo.TryGetProperty("$ref", out JsonElement referencia))
        {
            yield return referencia.GetString()!;
        }

        if (campo.TryGetProperty("oneOf", out JsonElement alternativas))
        {
            foreach (string referenciaDaAlternativa in alternativas.EnumerateArray().SelectMany(Referencias))
            {
                yield return referenciaDaAlternativa;
            }
        }
    }
}
