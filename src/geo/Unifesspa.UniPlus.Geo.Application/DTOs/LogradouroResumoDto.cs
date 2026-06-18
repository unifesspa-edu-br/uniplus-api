namespace Unifesspa.UniPlus.Geo.Application.DTOs;

using System.Text.Json.Serialization;

/// <summary>
/// DTO de item da coleção <c>GET /api/cidades/{codigoIbge}/logradouros</c>.
/// Representa o resultado enxuto do autocomplete de endereço, com CEP para salto ao
/// lookup completo de CEP.
/// </summary>
public sealed record LogradouroResumoDto(
    Guid Id,
    string Cep,
    string? Tipo,
    string Nome,
    string? NomeCompleto,
    string? Bairro,
    string CidadeCodigoIbge,
    string Uf)
{
    [JsonPropertyName("_links")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Links { get; init; }
}
