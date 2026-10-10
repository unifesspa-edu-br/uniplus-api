namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>
/// A resposta de um campo de endereço, na forma que o componente de endereço do formulário envia:
/// componentes de texto e a cidade escolhida pelo código IBGE.
/// </summary>
/// <param name="Procedencia">Como o endereço foi preenchido no formulário: pela busca do CEP no Geo ou à mão.</param>
public sealed record EnderecoDeclarado(
    string? Cep,
    string? Logradouro,
    string? Numero,
    string? Complemento,
    string? Bairro,
    string? Distrito,
    string? CidadeCodigoIbge,
    string? CidadeNome,
    string? CidadeUf,
    string? Latitude,
    string? Longitude,
    string? NivelResolucao,
    string? Procedencia)
{
    private static readonly string[] Componentes =
        ["cep", "logradouro", "numero", "complemento", "bairro", "distrito", "latitude", "longitude", "nivelResolucao", "origem"];

    private static readonly string[] ComponentesDaCidade = ["codigoIbge", "nome", "uf"];

    /// <summary>Recusa o que não for objeto com esses componentes, todos texto ou nulos.</summary>
    public static bool TentarLer(JsonElement valor, out EnderecoDeclarado endereco)
    {
        endereco = null!;
        if (!ComponentesValidos(valor, Componentes, permiteCidade: true))
        {
            return false;
        }

        JsonElement? cidade = valor.TryGetProperty("cidade", out JsonElement c) && c.ValueKind != JsonValueKind.Null ? c : null;
        if (cidade is { } objetoCidade && !ComponentesValidos(objetoCidade, ComponentesDaCidade, permiteCidade: false))
        {
            return false;
        }

        endereco = new EnderecoDeclarado(
            Texto(valor, "cep"), Texto(valor, "logradouro"), Texto(valor, "numero"), Texto(valor, "complemento"),
            Texto(valor, "bairro"), Texto(valor, "distrito"),
            cidade is { } ci ? Texto(ci, "codigoIbge") : null,
            cidade is { } cn ? Texto(cn, "nome") : null,
            cidade is { } cu ? Texto(cu, "uf") : null,
            Texto(valor, "latitude"), Texto(valor, "longitude"), Texto(valor, "nivelResolucao"), Texto(valor, "origem"));
        return true;
    }

    public JsonElement ParaJson()
    {
        JsonObject objeto = new()
        {
            ["cep"] = Cep,
            ["logradouro"] = Logradouro,
            ["numero"] = Numero,
            ["complemento"] = Complemento,
            ["bairro"] = Bairro,
            ["distrito"] = Distrito,
            ["cidade"] = CidadeCodigoIbge is null && CidadeNome is null && CidadeUf is null
                ? null
                : new JsonObject { ["codigoIbge"] = CidadeCodigoIbge, ["nome"] = CidadeNome, ["uf"] = CidadeUf },
            ["latitude"] = Latitude,
            ["longitude"] = Longitude,
            ["nivelResolucao"] = NivelResolucao,
            ["origem"] = Procedencia,
        };

        return JsonSerializer.SerializeToElement(objeto);
    }

    private static bool ComponentesValidos(JsonElement objeto, string[] componentes, bool permiteCidade) =>
        objeto.ValueKind == JsonValueKind.Object
        && objeto.EnumerateObject().All(p =>
            (permiteCidade && p.NameEquals("cidade"))
            || (componentes.Contains(p.Name, StringComparer.Ordinal) && p.Value.ValueKind is JsonValueKind.String or JsonValueKind.Null));

    private static string? Texto(JsonElement objeto, string componente) =>
        objeto.TryGetProperty(componente, out JsonElement v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
